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




type SocketStore(minorCount: int)=
    let mutable connectedSockets = 0
    let mutable majorSocket: Socket = null
    let mutable majorMinorDirectionDone = false
    let mutable minorMajorDirectionDone = false
    
//    let mutable merger = null
    let lockobj = new obj()
    let lockobj2 = new obj()
    let mutable minorSockets = Array.create minorCount (null)

    member x.MajorSocket 
        with get() = majorSocket
        and set y = majorSocket <- y
    member x.MinorSockets
        with get() = minorSockets
    member x.AddToMinorSockets(sock:Socket,sockIndex: int)=
        Monitor.Enter lockobj2
        connectedSockets <- (connectedSockets + 1)
        minorSockets.[sockIndex] <- sock
        Monitor.Exit lockobj2
    member x.AddToMinorSockets(sock:Socket)=
        Monitor.Enter lockobj2
        minorSockets.[connectedSockets] <- sock
        let con = connectedSockets
        connectedSockets <- (connectedSockets + 1)
        Monitor.Exit lockobj2
        con

    member x.ConnectedSockets
           with get()= connectedSockets
      
    member x.GrowMinorArray(growSize: int)=
        let gar = Array.create (growSize) (null)
        minorSockets <- Array.append minorSockets gar
    member x.SyncMajorReadDone() =
       // printfn "major read done"
        try
            majorSocket.Shutdown(SocketShutdown.Receive)
            for sock: Socket in minorSockets do
                sock.Shutdown(SocketShutdown.Send)
        with 
        | :? SocketException -> x.Close()
        | :? ObjectDisposedException -> ()
        majorMinorDirectionDone <- true
    //    printfn "in major read done: minormajordirectiondone: %b" minorMajorDirectionDone
        if minorMajorDirectionDone then
        //       printfn "closing the damn pipe!!"
            x.Close()
    member private x.SyncMinorReadDone()=
   //     printfn "minor read done"
        try
            majorSocket.Shutdown(SocketShutdown.Send)
            for sock: Socket in minorSockets do
                sock.Shutdown(SocketShutdown.Receive)
        with 
        | :? SocketException -> x.Close()
        | :? ObjectDisposedException -> ()

        minorMajorDirectionDone <- true
        if majorMinorDirectionDone then
            x.Close()           
    member private x.Syncer(f:unit->unit) =
        Monitor.Enter lockobj
        f()
        Monitor.Exit lockobj
    member x.Close()= 
        for sock: Socket in minorSockets do
            if sock <> null then
                sock.Close()
                
        if majorSocket <> null then
            majorSocket.Close()
        
    interface IDataPipe with
        member x.TotalTransferedData()= 
            printfn "stub"
            0UL  
    interface ISocketManager with
        member x.MajorReadDone() =
            x.Syncer(x.SyncMajorReadDone) 
        member x.MinorReadDone() =
            x.Syncer(x.SyncMinorReadDone)
        member x.SocketExceptionOccured sock exc  =
            x.Close()
    
