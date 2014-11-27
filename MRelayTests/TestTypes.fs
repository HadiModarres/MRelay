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
module TestTypes
open IDataPipe
open System
open System.Collections.Generic
open System.Collections

type t1(changes: Generic.Queue<int>)=
    let mutable total = 0
    interface IDataPipe with
        member x.TotalTransferedData()= // return the count of the bytes that have been passed through this object
            if changes.Count >0 then
                total <- total + changes.Dequeue()
            (uint64)total