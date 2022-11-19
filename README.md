# Core

## AwaitableCounter
A thread-safe counter that signals each time zero is crossed (in either direction)
via a completion task. Can be used in hierarchies of counters.

## EventQueueBase
An abstract base class that uses System.Threading.Channels to implement a single
threaded consumer of events.

## ExecutionQueue
A derivation of EventQueueBase. Events are IExecutable. Execute() is called
for each event.

## EventProcessor
A derivation of EventQueueBase. Events are passed to a provided implementation of 
IEventHandler.

## StateMachine
A derivation of EventQueueBase. Each event and current state are passed to a provided implementation of 
IStateEventHandler which returns updated state.

## Sequencer
Based on System.Threading.Channels, a scheduler that coordinates the execution 
of synchronous and asynchronous tasks using a hierarchical key that defines their 
relative concurrency. For example, if tasks are dispatched sequentially with the 
following hierarchical keys:
1. A
2. B
3. A.X
4. A.Y
5. A

then:

- tasks 1 and 2 will run in parallel;
- tasks 3 and 4 will both run in parallel after task 1 ends;
- task 5 will run after both tasks 3 and 4 end.
