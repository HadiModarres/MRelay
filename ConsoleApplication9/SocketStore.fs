    ///   2014 Sadegh Modarres   modarres.zadeh@gmail.com
/// 
///  This library is free software; you can redistribute it and/or
///  modify it under the terms of the GNU Lesser General Public
///  License as published by the Free Software Foundation; either
///  version 2.1 of the License, or (at your option) any later version.
/// 
///  This library is distributed in the hope that it will be useful,
///  but WITHOUT ANY WARRANTY; without even the implied warranty of
///  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
///  Lesser General Public License for more details.
module SocketStore

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections
open Splitter
open Merger
open IDataPipe
open ISocketManager
open System.Collections.Generic




type SocketStore()=
    let mutable connectedSockets = 0
    let mutable majorSocket: Socket = null
    let mutable majorReadStatus = true
//    let mutable minorMajorDirectionDone = false
    
    let minorSocketSets = new Generic.List<Socket[]>()
    let minorSetReadStatus = new Generic.List<Boolean>()
//    let mutable merger = null
    let lockobj = new obj()
    let lockobj2 = new obj()
//    let mutable minorSockets = Array.create minorCount (null)
    let mutable test = 0
    let pred(t: Boolean)=
        if t = true then
            false
        else
            true
    member x.ConnectedSockets
            with get() = connectedSockets
    member x.AddMinorSet(size: int)=
        let newset = Array.create size null
        connectedSockets <- 0
        ignore(minorSocketSets.Add(newset))
        ignore(minorSetReadStatus.Add(true))
    
    member x.GetLastMinorSet()=
        minorSocketSets.[minorSocketSets.Count-1] 
    member x.GetMinorSetAt(index: int)=
        minorSocketSets.[index] 

    member x.AddMinorSocket(socket: Socket)=
        Monitor.Enter lockobj2
        let ar = minorSocketSets.[minorSocketSets.Count-1] 
        ar.[connectedSockets] <- socket
        let con = connectedSockets
        connectedSockets <- (connectedSockets + 1)
        Monitor.Exit lockobj2
        con

    member x.AddMinorSocket(socket: Socket, index: int)=
        Monitor.Enter lockobj2
        let ar = minorSocketSets.[minorSocketSets.Count-1] 
        ar.[index] <- socket
        connectedSockets <- (connectedSockets + 1)
        Monitor.Exit lockobj2

    member x.MajorSocket 
        with get() = majorSocket
        and set y = majorSocket <- y
    
    member x.SyncMajorReadDone() =
        Monitor.Enter lockobj
        if majorReadStatus = true then
            try
                for mset in minorSocketSets.ToArray() do
                    for sock in mset do
                        sock.Shutdown(SocketShutdown.Send)
                        test <- (test+1)
                        printfn "df"

            with 
            | :? SocketException -> x.Close()
            | :? ObjectDisposedException -> ()
            majorReadStatus <- false
            if minorSetReadStatus.Count >0 then
                for i=minorSetReadStatus.Count-1 downto 0 do
                    let b = minorSetReadStatus.[i] 
                    if b = false then
                        x.Close(minorSocketSets.[i])
                      
        Monitor.Exit lockobj
    member private x.SyncMinorReadDone(set: Socket[])=
        Monitor.Enter lockobj
        let index = minorSocketSets.IndexOf(set)
        minorSetReadStatus.[index] <- false
        try
            let p = new Predicate<Boolean>(pred)
            if majorReadStatus = true && Array.TrueForAll(minorSetReadStatus.ToArray(),p) then
                majorSocket.Shutdown(SocketShutdown.Send)
            for sock in set do
                sock.Shutdown(SocketShutdown.Receive)
        with 
        | :? SocketException -> x.Close()
        | :? ObjectDisposedException -> ()
        if majorReadStatus = false then
            x.Close(set)
        Monitor.Exit lockobj
    
    member x.Close(set: Socket[])=
        let index = minorSocketSets.IndexOf(set)
        for sock in set do
                sock.Close()
        minorSetReadStatus.RemoveAt(index)
        minorSocketSets.RemoveAt(index)
        if majorReadStatus = false && minorSocketSets.Count = 0 then
            majorSocket.Close()
            majorReadStatus <- false
    
        
    member x.Close()= 
        for mset in minorSocketSets.ToArray() do
                for sock in mset do
                    if sock <> null then
                        try
                            sock.Shutdown(SocketShutdown.Both)
                        with
                        | _ -> ()
                        sock.Close()    
        minorSetReadStatus.Clear()
        minorSocketSets.Clear()
        if majorSocket <> null then
            majorSocket.Close()
        
    interface IDataPipe with
        member x.TotalTransferedData()= 
            printfn "stub"
            0UL  
    interface ISocketManager with
        member x.MajorReadDone() =
            x.SyncMajorReadDone() 
        member x.MinorReadDone(set: Socket[]) =
            x.SyncMinorReadDone(set)
        member x.SocketExceptionOccured sock exc  =
            raise(exc)
            x.Close()
    
