﻿module DataDelegate

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

[<AllowNullLiteral>]
type IDataDelegate= 
    abstract ReceivedDataHash: byte[] -> unit
    abstract SentDataHash: byte[] ->  unit




