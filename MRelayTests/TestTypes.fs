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