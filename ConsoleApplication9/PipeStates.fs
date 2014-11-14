module PipeStates

type PipeState =
| AcceptingConnections = 0
| Connecting = 1
| Relaying = 2
| ThrottlingUp_PausingSplitter = 3
| ThrottlingUp_PausingMerger = 4
| ThrottlingUp_ConnectingFirstConnection = 5
| ThrottlingUp_ExchangingInfo = 6
| ThrottlingUp_ConnectingAll = 7
| ThrottlingUp_SendingPauseCommand = 8
| ThrottlingUp_ReceivingPauseResponse = 9
| ThrottlingUp_ReadingThrottleCommand = 10
| ThrottlingUp_ReadingSyncInfo = 11
| ThrottlingUp_SendingSyncOk = 12


