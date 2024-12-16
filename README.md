# MetaFac.Threading

A collection of inter-thread coordination types with implementations based on
System.Threading.Channels and LMAX Disruptor.

## ExecutionQueue
A multi-writer single-reader queue of events where events are IExecutable. Execute()
is called for each event.

## EventProcessor
A multi-writer single-reader queue of events where that are passed to a provided 
implementation of IEventHandler.

## StateMachine
A multi-writer single-reader queue of events. Each event and current state are 
passed to a provided implementation of IStateEventHandler which returns updated 
state.

## AwaitableCounter
A thread-safe counter that signals each time zero is crossed (in either direction)
via a completion task. Can be used in hierarchies of counters.

## ValueTaskQueue
A multi-writer single-reader queue of synchronous or asynchronous tasks where that is
called for each event.
