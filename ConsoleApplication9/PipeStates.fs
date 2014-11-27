// Copyright 2014 Hadi Modarres
// modarres.zadeh@gmail.com
//
// This file is part of MRelay.
// MRelay is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MRelay is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
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
| Closing = 14
