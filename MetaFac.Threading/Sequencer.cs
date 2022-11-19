using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MetaFac.Threading
{
    public sealed class Sequencer : EventQueueBase<SequencerEvent>
    {
        private enum SequencerNodeStatus
        {
            Initial,
            Running,
            Closing,
            Closed,
        }

        private struct SequencerNodeState
        {
            public SequencerNodeStatus Status;
            public Sequencer Sequencer;
        }

        private readonly CancellationToken _shutdownToken;
        private readonly int _level;
        private readonly Sequencer? _parent;
        private readonly ISequencerConfiguration _configuration;

        // single-threaded state
        private readonly Queue<SequencerEvent> _queue = new Queue<SequencerEvent>();
        private readonly SequencerNodeState[] _subNodes;
        private int _subNodesRunning = 0;
        private int _subNodesClosing = 0;

        private static int GetSubNodesWidth(int[] widths, int level)
        {
            if (!(widths is null) && (level < widths.Length))
            {
                return widths[level];
            }
            else
            {
                return Environment.ProcessorCount;
            }
        }

        private Sequencer(CancellationToken shutdownToken, Sequencer? parent, int level, ISequencerConfiguration configuration)
            : base(shutdownToken)
        {
            _shutdownToken = shutdownToken;
            _level = level;
            _parent = parent;
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _subNodes = new SequencerNodeState[GetSubNodesWidth(_configuration.LevelWidths, level)];
        }

        public Sequencer(CancellationToken shutdownToken, ISequencerConfiguration? configuration = null)
            : this(shutdownToken, null, 0, (configuration is null) ? new SequencerConfiguration() : new SequencerConfiguration(configuration))
        {
        }

        /// <summary>
        /// Tries to close any running children.
        /// </summary>
        /// <returns>True if running children found; false otherwise.</returns>
        private async ValueTask CloseRunningChildren()
        {
            for (int slot = 0; slot < _subNodes.Length; slot++)
            {
                if (_subNodes[slot].Status == SequencerNodeStatus.Running)
                {
                    await _subNodes[slot].Sequencer.EnqueueAsync(new SequencerEvent(SequencerEventKind.EmptyRequest, slot)).ConfigureAwait(false);
                    _subNodes[slot].Status = SequencerNodeStatus.Closing;
                    _subNodesClosing++;
                    _subNodesRunning--;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CalculateChildSlot(int hash)
        {
            int length = _subNodes.Length;
            int slot = hash % length;
            if (slot < 0) slot += length;
            Debug.Assert(slot >= 0 && slot < length);
            return slot;
        }

        protected sealed override void OnDequeued(SequencerEvent @event)
        {
            OnDequeueInternal(@event).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        private async ValueTask OnDequeueInternal(SequencerEvent received)
        {
            // enqueue workitems and empty requests
            // process empty responses immediately
            switch (received.Kind)
            {
                case SequencerEventKind.EmptyRequest:
                    _queue.Enqueue(received);
                    break;
                case SequencerEventKind.WorkItem:
                    // enqueue
                    _queue.Enqueue(received);
                    break;
                case SequencerEventKind.EmptyResponse:
                    // cleanup child
                    _subNodes[received.Slot].Status = SequencerNodeStatus.Closed;
                    _subNodesClosing--;
                    break;
                default:
                    throw new NotImplementedException();
            }

            // dequeue as much as we can
            // execute the work item if queue empty and no children
            // otherwise enqueue it for this level or forward it to
            // the child level
            SequencerEvent dequeued;
            do
            {
                dequeued = default;
                if (_queue.Count > 0)
                {
                    var nextEvent = _queue.Peek();
                    switch (nextEvent.Kind)
                    {
                        case SequencerEventKind.EmptyRequest:
                            if (_subNodesRunning > 0)
                            {
                                await CloseRunningChildren().ConfigureAwait(false);
                            }
                            else if (_subNodesClosing > 0)
                            {
                                // subnodes still closing
                            }
                            else
                            {
                                // no children running or closing - continue
                                dequeued = _queue.Dequeue();
                                if (_parent is not null)
                                {
                                    await _parent.EnqueueAsync(new SequencerEvent(SequencerEventKind.EmptyResponse, nextEvent.Slot)).ConfigureAwait(false);
                                }
                            }
                            break;
                        case SequencerEventKind.WorkItem:
                            if (nextEvent.Level > _level)
                            {
                                // for child level
                                dequeued = _queue.Dequeue();
                                int slot = CalculateChildSlot(nextEvent.Keys![_level]);
                                var status = _subNodes[slot].Status;
                                switch (status)
                                {
                                    case SequencerNodeStatus.Initial:
                                        _subNodes[slot].Sequencer = new Sequencer(_shutdownToken, this, _level + 1, _configuration);
                                        _subNodes[slot].Status = SequencerNodeStatus.Running;
                                        _subNodesRunning++;
                                        break;
                                    case SequencerNodeStatus.Running:
                                        // good to go
                                        break;
                                    case SequencerNodeStatus.Closed:
                                        _subNodes[slot].Status = SequencerNodeStatus.Running;
                                        _subNodesRunning++;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException(nameof(status), status, null);
                                }
                                await _subNodes[slot].Sequencer.EnqueueAsync(dequeued).ConfigureAwait(false);
                            }
                            else
                            {
                                // for this level
                                if (_subNodesRunning > 0)
                                {
                                    await CloseRunningChildren().ConfigureAwait(false);
                                }
                                else if (_subNodesClosing > 0)
                                {
                                    // subnodes still closing
                                }
                                else
                                {
                                    // no children running. run!
                                    dequeued = _queue.Dequeue();
                                    if (nextEvent.WorkItem is not null)
                                    {
                                        try
                                        {
                                            await nextEvent.WorkItem.ExecuteAsync(_shutdownToken).ConfigureAwait(false);
                                        }
                                        catch (Exception)
                                        {
                                        }
                                        finally
                                        {
                                            await nextEvent.WorkItem.DisposeAsync().ConfigureAwait(false);
                                        }
                                    }
                                }
                            }
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            } while (dequeued.Kind != SequencerEventKind.Undefined);
        }

        public async ValueTask SequenceWorkItemAsync(int[] keys, IExecutable workItem)
        {
            if (keys is null) throw new ArgumentNullException(nameof(keys));
            if (workItem is null) throw new ArgumentNullException(nameof(workItem));
            await EnqueueAsync(new SequencerEvent(keys.Length, keys, workItem)).ConfigureAwait(false);
        }

        //protected sealed override void OnCanceled(SequencerEvent @event)
        //{
        //    if (@event.Kind == SequencerEventKind.WorkItem)
        //    {
        //        var workItem = @event.WorkItem;
        //        try
        //        {
        //            workItem?.Cancel(null);
        //        }
        //        catch (Exception)
        //        {
        //        }
        //    }
        //}

    }
}