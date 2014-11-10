module PipeStates

type PipeState =
| AcceptingConnections = 0
| Connecting = 1
| Relaying = 2
| ThrottlingUp = 3

