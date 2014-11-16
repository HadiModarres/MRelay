module PipeStates

type PipeState =
| AcceptingConnections = 0
| Connecting = 1
| Relaying = 2
| ThrottlingUp_PausingSplitter = 3
| ThrottlingUp_PausingMerger = 4
| ThrottlingUp_ConnectingFirstConnection = 5
| ThrottlingUp_ConnectingAll = 7
| ThrottlingUp_ReadingSyncInfo = 10
| ThrottlingUp_SendingSyncInfo = 11
| ThrottlingUp_SendingSyncOk = 12
| ThrottlingUp_ReceivingSyncOk = 13

