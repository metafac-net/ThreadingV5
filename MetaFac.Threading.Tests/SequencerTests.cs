using FluentAssertions;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MetaFac.Threading.Tests
{
    public class SequencerTests
    {
        [Fact]
        public async Task EnqueueAfterCancelled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            using (var sequencer = new Sequencer(cts.Token))
            {
                var workItem = new ExecutableItem<int, int>(0, CancellationToken.None, DoNothing);
                var ex = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    await sequencer.SequenceWorkItemAsync(new int[0], workItem);
                });
                ex.Message.Should().Be("The operation was canceled.");
            }
        }

        private ValueTask<int> DoNothing(int input, CancellationToken token)
        {
            return new ValueTask<int>(input);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public async Task Enqueue_1(int depth)
        {
            int[] maxKeys = (new string[] { "A", "1", "a", "M" }).Select(s => s.GetHashCode()).ToArray();
            Assert.True(depth >= 0 && depth <= maxKeys.Length);

            var timeout = Debugger.IsAttached ? TimeSpan.FromSeconds(300) : TimeSpan.FromSeconds(10);
            using var cts = new CancellationTokenSource(timeout);
            using (var sequencer = new Sequencer(cts.Token))
            {
                var keys = maxKeys.Take(depth).ToArray();

                var workItem = new ExecutableItem<int, int>(0, CancellationToken.None, DoNothing);
                await sequencer.SequenceWorkItemAsync(keys, workItem);

                var lastItem = new ExecutableItem<int, int>(0, CancellationToken.None, DoNothing);
                await sequencer.SequenceWorkItemAsync(new int[0], lastItem);
                var result2 = await lastItem.GetTask();
                cts.Cancel();
            }
        }

        [Theory]
        [InlineData(1_000_000, 0)]
        [InlineData(1_000_000, 1)]
        [InlineData(1_000_000, 2)]
        [InlineData(1_000_000, 3)]
        [InlineData(1_000_000, 4)]
        public async Task Enqueue_Many_Serial(int iterations, int depth)
        {
            int[] maxKeys = (new string[] { "A", "1", "a", "M" }).Select(s => s.GetHashCode()).ToArray();
            Assert.True(depth >= 0 && depth <= maxKeys.Length);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using (var sequencer = new Sequencer(cts.Token))
            {
                var keys = maxKeys.Take(depth).ToArray();

                for (int i = 0; i < iterations; i++)
                {
                    var item = new ExecutableItem<int, int>(0, CancellationToken.None, DoNothing);
                    await sequencer.SequenceWorkItemAsync(keys, item);
                }

                var lastItem = new ExecutableItem<int, int>(0, CancellationToken.None, DoNothing);
                await sequencer.SequenceWorkItemAsync(new int[0], lastItem);
                var result = await lastItem.GetTask();
                cts.Cancel();
            }
        }

        //[Theory]
        //[InlineData(100_000)]
        //public async Task Enqueue_L1_Many_Parallel(int iterations)
        //{
        //    var logger = new TestLogger();
        //    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        //    using var sequencer = new Sequencer(logger, cts.Token);

        //    const int L1Keys = 16;
        //    string xxx;
        //    var keys = new string[] { "A" };
        //    for (int i = 0; i < iterations; i++)
        //    {
        //        queue.SequenceWorkItem(keys, new WorkItemTask());
        //    }
        //    var lastItem = new WorkItemTask();
        //    queue.SequenceWorkItem(new int[0], lastItem);
        //    bool result = await lastItem.GetTask();
        //    cts.Cancel();
        //}

        private ValueTask<int> DoWork(int input, CancellationToken token)
        {
            return new ValueTask<int>(input * input);
        }

        [Fact]
        public async Task SequenceL0()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using (var sequencer = new Sequencer(cts.Token))
            {
                var workItem = new ExecutableItem<int, int>(2, cts.Token, DoWork);
                var keyHashes = new int[0];
                await sequencer.SequenceWorkItemAsync(keyHashes, workItem);
                var result = await workItem.GetTask();
                result.Should().Be(4);
            }
        }

        [Fact]
        public async Task SequenceL1()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using (var sequencer = new Sequencer(cts.Token))
            {
                var workItem = new ExecutableItem<int, int>(2, cts.Token, DoWork);
                int[] keyHashes = new string[] { "a" }.Select(s => s.GetHashCode()).ToArray();
                await sequencer.SequenceWorkItemAsync(keyHashes, workItem);
                var result = await workItem.GetTask();
                result.Should().Be(4);
            }
        }

        private readonly struct Handle
        {
            public readonly int[] KeyHashes;

            public Handle(int[] keyHashes)
            {
                KeyHashes = keyHashes;
            }
        }
        private class TestMsg
        {
            public readonly int L;
            public readonly int A = -1;
            public readonly int B = -1;
            public readonly int C = -1;
            public readonly int D = -1;
            public readonly int N;
            public TestMsg(char notUsed, int seqnum)
            {
                L = 0;
                N = seqnum;
            }
            public TestMsg(char notUsed, int seqnum, int a)
            {
                L = 1;
                A = a;
                N = seqnum;
            }
            public TestMsg(char notUsed, int seqnum, int a, int b)
            {
                L = 2;
                A = a;
                B = b;
                N = seqnum;
            }
            public TestMsg(char notUsed, int seqnum, int a, int b, int c)
            {
                L = 3;
                A = a;
                B = b;
                C = c;
                N = seqnum;
            }
            public TestMsg(char notUsed, int seqnum, int a, int b, int c, int d)
            {
                L = 4;
                A = a;
                B = b;
                C = c;
                D = d;
                N = seqnum;
            }
            private string NStr(int n)
            {
                if ((n >= 0) && (n <= 9))
                    return " " + n.ToString();
                else
                    return n.ToString();
            }
            public string Title { get { return String.Format("L{0}[{1},{2},{3}]", L, NStr(A), NStr(B), NStr(C)); } }
        }
        [Fact]
        public async Task TestSequencing()
        {
            const int InitSeqNum = 1_000_000;
            const int scaleFactor = 50; // => 50^3 or 125,000 tasks

            long seqErrors = 0;
            int dispatchCount = 0;
            int callbackCount = 0;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using (var sequencer = new Sequencer(cts.Token))
            {
                try
                {
                    // setup Dispatcher handles
                    Handle handle0 = new Handle((new string[0]).Select(s => s.GetHashCode()).ToArray());
                    Handle[] handle1 = new Handle[scaleFactor];
                    Handle[,] handle2 = new Handle[scaleFactor, scaleFactor];
                    Handle[,,] handle3 = new Handle[scaleFactor, scaleFactor, scaleFactor];
                    TestMsg prev_msg0 = new TestMsg('0', 0);
                    TestMsg[] prev_msg1 = new TestMsg[scaleFactor];
                    TestMsg[,] prev_msg2 = new TestMsg[scaleFactor, scaleFactor];
                    TestMsg[,,] prev_msg3 = new TestMsg[scaleFactor, scaleFactor, scaleFactor];
                    for (int a = 0; a < scaleFactor; a++)
                    {
                        string aKey = "a" + a.ToString();
                        handle1[a] = new Handle((new string[] { aKey }).Select(s => s.GetHashCode()).ToArray());
                        for (int b = 0; b < scaleFactor; b++)
                        {
                            string bKey = "b" + b.ToString();
                            handle2[a, b] = new Handle((new string[] { aKey, bKey }).Select(s => s.GetHashCode()).ToArray());
                            for (int c = 0; c < scaleFactor; c++)
                            {
                                string cKey = "c" + c.ToString();
                                handle3[a, b, c] = new Handle((new string[] { aKey, bKey, cKey }).Select(s => s.GetHashCode()).ToArray());
                            }
                        }
                    }

                    // dispatch some events and check sequence
                    void TestSeqNum(TestMsg thisMsg, TestMsg prevMsg)
                    {
                        try
                        {
                            int prevN = prevMsg?.N ?? 0;
                            thisMsg.N.Should().BeGreaterThan(prevN);
                        }
                        catch (Exception)
                        {
                            // failed
                            Interlocked.Increment(ref seqErrors);
                            throw;
                        }
                    };

                    ValueTask<int> callback(TestMsg state, CancellationToken ct)
                    {
                        Interlocked.Increment(ref callbackCount);
                        TestMsg msg = (TestMsg)state;
                        Thread.Sleep(3 - (msg.L * 1));
                        // test sequence
                        // - seqnum must be greater than parents and all children
                        if (msg.L == 0)
                        {
                            TestSeqNum(msg, prev_msg0);
                            for (int a = 0; a < scaleFactor; a++)
                            {
                                TestSeqNum(msg, prev_msg1[a]);
                                for (int b = 0; b < scaleFactor; b++)
                                {
                                    TestSeqNum(msg, prev_msg2[a, b]);
                                    for (int c = 0; c < scaleFactor; c++)
                                    {
                                        TestSeqNum(msg, prev_msg3[a, b, c]);
                                    }
                                }
                            }
                            prev_msg0 = msg;
                        }
                        if (msg.L == 1)
                        {
                            TestSeqNum(msg, prev_msg0);
                            TestSeqNum(msg, prev_msg1[msg.A]);
                            for (int b = 0; b < scaleFactor; b++)
                            {
                                TestSeqNum(msg, prev_msg2[msg.A, b]);
                                for (int c = 0; c < scaleFactor; c++)
                                {
                                    TestSeqNum(msg, prev_msg3[msg.A, b, c]);
                                }
                            }
                            prev_msg1[msg.A] = msg;
                        }
                        if (msg.L == 2)
                        {
                            TestSeqNum(msg, prev_msg0);
                            TestSeqNum(msg, prev_msg1[msg.A]);
                            TestSeqNum(msg, prev_msg2[msg.A, msg.B]);
                            for (int c = 0; c < scaleFactor; c++)
                            {
                                TestSeqNum(msg, prev_msg3[msg.A, msg.B, c]);
                            }
                            prev_msg2[msg.A, msg.B] = msg;
                        }
                        if (msg.L == 3)
                        {
                            TestSeqNum(msg, prev_msg0);
                            TestSeqNum(msg, prev_msg1[msg.A]);
                            TestSeqNum(msg, prev_msg2[msg.A, msg.B]);
                            TestSeqNum(msg, prev_msg3[msg.A, msg.B, msg.C]);
                            prev_msg3[msg.A, msg.B, msg.C] = msg;
                        }
                        return new ValueTask<int>(0);
                    };

                    int seqnum = InitSeqNum;
                    await sequencer.SequenceWorkItemAsync(handle0.KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('0', seqnum++), cts.Token, callback));
                    for (int a = 0; a < scaleFactor; a++)
                    {
                        await sequencer.SequenceWorkItemAsync(handle1[a].KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('1', seqnum++, a), cts.Token, callback));
                        for (int b = 0; b < scaleFactor; b++)
                        {
                            await sequencer.SequenceWorkItemAsync(handle2[a, b].KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('2', seqnum++, a, b), cts.Token, callback));
                            for (int c = 0; c < scaleFactor; c++)
                            {
                                await sequencer.SequenceWorkItemAsync(handle3[a, b, c].KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('3', seqnum++, a, b, c), cts.Token, callback));
                            }
                            await sequencer.SequenceWorkItemAsync(handle2[a, b].KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('2', seqnum++, a, b), cts.Token, callback));
                        }
                        await sequencer.SequenceWorkItemAsync(handle1[a].KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('1', seqnum++, a), cts.Token, callback));
                    }
                    var lastItem = new ExecutableItem<TestMsg, int>(new TestMsg('0', seqnum++), cts.Token, callback);
                    var lastTask = lastItem.GetTask();
                    await sequencer.SequenceWorkItemAsync(handle0.KeyHashes, lastItem);
                    dispatchCount = (seqnum - InitSeqNum);
                    var result = await lastTask;
                }
                finally
                {
                    // test
                    dispatchCount.Should().Be(callbackCount);
                    Interlocked.Add(ref seqErrors, 0).Should().Be(0);
                }
            }
        }

        [Fact]
        public async Task TestScalability()
        {
            const int InitSeqNum = 1_000_000;
            const int scaleFactor = 40; // => 40^4 (or 2,560,000) tasks

            int dispatchCount = 0;
            int callbackCount = 0;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using (var sequencer = new Sequencer(cts.Token))
            {
                try
                {
                    // setup Dispatcher handles
                    Handle handle0 = new Handle((new string[0]).Select(s => s.GetHashCode()).ToArray());
                    Handle[] handle1 = new Handle[scaleFactor];
                    Handle[,] handle2 = new Handle[scaleFactor, scaleFactor];
                    Handle[,,] handle3 = new Handle[scaleFactor, scaleFactor, scaleFactor];
                    Handle[,,,] handle4 = new Handle[scaleFactor, scaleFactor, scaleFactor, scaleFactor];
                    for (int a = 0; a < scaleFactor; a++)
                    {
                        string aKey = "a" + a.ToString();
                        handle1[a] = new Handle((new string[] { aKey }).Select(s => s.GetHashCode()).ToArray());
                        for (int b = 0; b < scaleFactor; b++)
                        {
                            string bKey = "b" + b.ToString();
                            handle2[a, b] = new Handle((new string[] { aKey, bKey }).Select(s => s.GetHashCode()).ToArray());
                            for (int c = 0; c < scaleFactor; c++)
                            {
                                string cKey = "c" + c.ToString();
                                handle3[a, b, c] = new Handle((new string[] { aKey, bKey, cKey }).Select(s => s.GetHashCode()).ToArray());
                                for (int d = 0; d < scaleFactor; d++)
                                {
                                    string dKey = "d" + d.ToString();
                                    handle4[a, b, c, d] = new Handle((new string[] { aKey, bKey, cKey, dKey }).Select(s => s.GetHashCode()).ToArray());
                                }
                            }
                        }
                    }

                    // dispatch some events and check sequence
                    ValueTask<int> callback(TestMsg state, CancellationToken ct)
                    {
                        Interlocked.Increment(ref callbackCount);
                        return new ValueTask<int>(0);
                    };
                    int seqnum = InitSeqNum;
                    await sequencer.SequenceWorkItemAsync(handle0.KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('0', seqnum++), cts.Token, callback));
                    for (int a = 0; a < scaleFactor; a++)
                    {
                        await sequencer.SequenceWorkItemAsync(handle1[a].KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('1', seqnum++, a), cts.Token, callback));
                        for (int b = 0; b < scaleFactor; b++)
                        {
                            await sequencer.SequenceWorkItemAsync(handle2[a, b].KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('2', seqnum++, a, b), cts.Token, callback));
                            for (int c = 0; c < scaleFactor; c++)
                            {
                                await sequencer.SequenceWorkItemAsync(handle3[a, b, c].KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('3', seqnum++, a, b, c), cts.Token, callback));
                                for (int d = 0; d < scaleFactor; d++)
                                {
                                    await sequencer.SequenceWorkItemAsync(handle4[a, b, c, d].KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('4', seqnum++, a, b, c, d), cts.Token, callback));
                                }
                                await sequencer.SequenceWorkItemAsync(handle3[a, b, c].KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('3', seqnum++, a, b, c), cts.Token, callback));
                            }
                            await sequencer.SequenceWorkItemAsync(handle2[a, b].KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('2', seqnum++, a, b), cts.Token, callback));
                        }
                        await sequencer.SequenceWorkItemAsync(handle1[a].KeyHashes, new ExecutableItem<TestMsg, int>(new TestMsg('1', seqnum++, a), cts.Token, callback));
                    }
                    var lastItem = new ExecutableItem<TestMsg, int>(new TestMsg('0', seqnum++), cts.Token, callback);
                    await sequencer.SequenceWorkItemAsync(handle0.KeyHashes, lastItem);
                    dispatchCount = (seqnum - InitSeqNum);
                    var result = await lastItem.GetTask();
                }
                finally
                {
                    // test
                    dispatchCount.Should().Be(callbackCount);
                }
            }
        }

        [Fact]
        public async Task TestParallelism()
        {
            Int32 counter = 10_000;
            // dispatches a series of serial and parallel tasks
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using (var sequencer = new Sequencer(cts.Token))
            {
                Handle key0_root = new Handle(new int[0]);
                Handle key1_slow = new Handle(new int[] { 1 });
                Handle key1_norm = new Handle(new int[] { 2 });
                Handle key1_fast = new Handle(new int[] { 3 });
                // start first global task
                await sequencer.SequenceWorkItemAsync(key0_root.KeyHashes, new ExecutableItem<int, int>(0, cts.Token, (inp, ct) =>
                {
                    int count1 = Interlocked.Increment(ref counter);
                    Thread.Sleep(1000);
                    int count2 = Interlocked.Increment(ref counter);
                    return 0;
                }));
                // start 1 slow (3 second) task
                for (int i = 0; i < 1; i++)
                {
                    await sequencer.SequenceWorkItemAsync(key1_slow.KeyHashes, new ExecutableItem<string, int>(i.ToString(), cts.Token, (name, ct) =>
                    {
                        int count1 = Interlocked.Increment(ref counter);
                        Thread.Sleep(3000); // 3 secs
                        int count2 = Interlocked.Increment(ref counter);
                        return 0;
                    }));
                }
                // start 3 normal (1 second) tasks
                for (int i = 0; i < 3; i++)
                {
                    await sequencer.SequenceWorkItemAsync(key1_norm.KeyHashes, new ExecutableItem<string, int>(i.ToString(), cts.Token, (name, ct) =>
                    {
                        int count1 = Interlocked.Increment(ref counter);
                        Thread.Sleep(1000); // 1 sec
                        int count2 = Interlocked.Increment(ref counter);
                        return 0;
                    }));
                }
                // start 10 fast (0.1 second) tasks
                for (int i = 0; i < 10; i++)
                {
                    await sequencer.SequenceWorkItemAsync(key1_fast.KeyHashes, new ExecutableItem<string, int>(i.ToString(), cts.Token, (name, ct) =>
                    {
                        int count1 = Interlocked.Increment(ref counter);
                        Thread.Sleep(100); // 0.1 sec
                        int count2 = Interlocked.Increment(ref counter);
                        return 0;
                    }));
                }
                // start last global task
                var lastItem = new ExecutableItem<int, int>(0, cts.Token, (inp, ct) =>
                {
                    Thread.Sleep(1000);
                    int count2 = Interlocked.Increment(ref counter);
                    return new ValueTask<int>(0);
                });
                await sequencer.SequenceWorkItemAsync(key0_root.KeyHashes, lastItem);
                var result = await lastItem.GetTask();
            }
        }
    }
}