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
module Pipe

open System
open System.Threading
open SocketStore
open PipeStates
open System.Net.Sockets
open System.Net
open IPipeManager
open Splitter
open Merger
open System.Collections
open ICycle
open CycleManager
open IDataPipe

//let mutable totPipes = 0
let mutable reachedRelay = 0
let mutable totPipes = 0

[<AllowNullLiteral>] 
type Pipe(pipeManager: IPipeManager,minorCount: int,isMajorSocketOnRelayListenSide: bool)as this=  // saves state info for each tcp pipe and implements MRelay protocol
    let mutable state = PipeState.AcceptingConnections
    let majorReadCallback = new AsyncCallback(this.DataReceiveToMajorSocket)
    let majorSendCallback = new AsyncCallback(this.DataSendToMajorSocket)
    let minorReadCallback = new AsyncCallback(this.DataReceiveToMinorSocket)
    let minorSendCallback = new AsyncCallback(this.DataSendToMinorSocket)
    let socketStore = new SocketStore(this.SocketStoreClosed)
    let mutable guid: byte[] = null
    let mutable throttleUpSize = 0
    let mergerChain = new CycleManager()
    let splitterChain = new CycleManager()
    let timerCallback = new TimerCallback(this.ThrottleTest)
    let timer = new Threading.Timer(timerCallback)
    let lockobj = new obj()
    let throttleCycleDelay = 3 //
    let mutable totalTransferedBytes = 0UL 
    let pendingSockets = Generic.List<Socket>()
    
    let chainAggregateData (sp:ICycle[]) = 
        Array.fold (fun (acc:uint64) (d:ICycle)-> acc+(d:?>IDataPipe).TotalTransferedData()) 0UL sp

    do
        socketStore.AddMinorSet(minorCount)
//        totPipes <- totPipes+1
//        printfn "tot pipes: %i" totPipes

//    do 
//        ignore(timer.Change(6000,Timeout.Infinite))
//    

    member this.SocketStoreClosed()=
        Monitor.Enter lockobj
        state <- PipeState.Closing
        pipeManager.pipeDone(this)
        Monitor.Exit lockobj
    
    
    member this.ThrottleTest(timerObj: obj)=
        ignore(this.ThrottleUp(20))
    member this.GUID 
        with get() = guid
        and set(gui: byte[]) = guid <- gui

    member public this.NewSocketReceived(socket: Socket)= 
        Monitor.Enter lockobj
        match state with
        | PipeState.AcceptingConnections when isMajorSocketOnRelayListenSide=true -> 
            state <- PipeState.Connecting
            socketStore.MajorSocket <- socket
            
            for i = 0 to minorCount-1 do
                pipeManager.needAConnection(this) 
            ()
        | PipeState.AcceptingConnections when isMajorSocketOnRelayListenSide=false -> 
            this.ReadSocketIndex(socket)
            ()
        | PipeState.Relaying when isMajorSocketOnRelayListenSide=false ->
            state <- PipeState.ThrottlingUp_ReadingSyncInfo
            let buf = Array.create 6 (new Byte())
            try
                ignore(socket.BeginReceive(buf,0,buf.GetLength(0),SocketFlags.None,minorReadCallback,(socket,buf)))
            with 
            | :? SocketException -> socketStore.Close()
            | :? ObjectDisposedException -> socketStore.Close()
            () 
        | PipeState.ThrottlingUp_ConnectingAll when isMajorSocketOnRelayListenSide = false ->
            ignore(this.ReadSocketIndex(socket))
            ()
        | PipeState.Closing ->
            socket.Close()
        | _ -> ()
        Monitor.Exit lockobj

    member public this.SocketConnected(socket: Socket)=
        Monitor.Enter lockobj
        
        ignore(pendingSockets.Remove(socket))

        match state with
        | PipeState.Connecting when isMajorSocketOnRelayListenSide= false ->
            socketStore.MajorSocket <- socket;
            this.InitRelay()
            ()
        | PipeState.Connecting when isMajorSocketOnRelayListenSide= true ->
            let index = socketStore.AddMinorSocket(socket);
            let buf = Array.create 1 (new Byte());
            buf.[0] <- (byte) index;
            try
                ignore(socket.BeginSend(buf,0,1,SocketFlags.None,null,null)) ;// check, do beginSend(data1,socket1);beginSend(data2,socket1) make data1 be sent first and then data2 for sure?
                if socketStore.ConnectedSockets = socketStore.GetLastMinorSet().GetLength(0) then
                    this.InitRelay()
            with 
            | :? SocketException -> socketStore.Close()
            | :? ObjectDisposedException -> socketStore.Close()
            ()
        | PipeState.ThrottlingUp_ConnectingFirstConnection when isMajorSocketOnRelayListenSide = true ->
            state <- PipeState.ThrottlingUp_SendingSyncInfo
        
            socketStore.AddMinorSet(throttleUpSize)
            let index = socketStore.AddMinorSocket(socket)
            let currentSplitterCycleNumber = splitterChain.Pause(fun ()->())
            let size = throttleUpSize

            splitterChain.AddToFutureMembers(currentSplitterCycleNumber+throttleCycleDelay)
            let buf = Array.create 6 (new Byte())
            buf.[0] <- (byte) index
            buf.[1] <- (byte) size
            Array.blit (BitConverter.GetBytes(currentSplitterCycleNumber+throttleCycleDelay)) 0 buf 2 4
            try
                ignore(socket.BeginSend(buf,0,buf.GetLength(0),SocketFlags.None,null,null))
                state <- PipeState.ThrottlingUp_ReadingSyncInfo
                let buf2 = Array.create 4 (new Byte())
                ignore(socket.BeginReceive(buf2,0,buf2.GetLength(0),SocketFlags.None,minorReadCallback,(socket,buf2)))
            with 
            | :? SocketException -> socketStore.Close()
            | :? ObjectDisposedException -> socketStore.Close()
            ()
        | PipeState.ThrottlingUp_ConnectingAll when isMajorSocketOnRelayListenSide = true ->
            let index = socketStore.AddMinorSocket(socket);
            let buf = Array.create 1 (new Byte());
            buf.[0] <- (byte) index;
            try
                ignore(socket.BeginSend(buf,0,1,SocketFlags.None,null,null)) ;  // check, do beginSend(data1,socket1);beginSend(data2,socket1) make data1 be sent first and then data2 for sure?
            with 
            | :? SocketException -> socketStore.Close()
            | :? ObjectDisposedException -> socketStore.Close()
            if socketStore.ConnectedSockets = socketStore.GetLastMinorSet().GetLength(0) then
                state <- PipeState.Relaying
                let s = new StreamSplitter(socketStore,socketStore.MajorSocket,socketStore.GetLastMinorSet(),pipeManager.getDynamicSegmentSize(),pipeManager.getDynamicSocketBufferSize())
                let m = new StreamMerger(socketStore,socketStore.MajorSocket,socketStore.GetLastMinorSet(),pipeManager.getDynamicSegmentSize(),pipeManager.getDynamicSocketBufferSize())
                splitterChain.AddToFutureMembers(s)
                mergerChain.AddToFutureMembers(m) 
                splitterChain.Resume()
                mergerChain.Resume()

            ()  
        | PipeState.Closing ->
            socket.Close()
        | _ -> ()
        Monitor.Exit lockobj
    member public this.ThrottleUp(byHowManyConnections: int)=
        Monitor.Enter lockobj
        match state with
        |PipeState.Relaying when isMajorSocketOnRelayListenSide= true ->
            throttleUpSize <- byHowManyConnections
            state <- PipeState.ThrottlingUp_ConnectingFirstConnection
            pipeManager.needAConnection(this)
            ()
        | _ -> 
            ()
        Monitor.Exit lockobj

    member private this.SplitterPaused()=
        Monitor.Enter lockobj
        match state with
        | PipeState.ThrottlingUp_PausingSplitter when isMajorSocketOnRelayListenSide= true ->
            state <- PipeState.ThrottlingUp_ConnectingFirstConnection
            pipeManager.needAConnection(this)
            ()
        | PipeState.ThrottlingUp_PausingSplitter when isMajorSocketOnRelayListenSide= false ->
            ()
        | _ -> ()
        Monitor.Exit lockobj
    member private this.MergerPaused()=
        Monitor.Enter lockobj
        match state with
        | PipeState.ThrottlingUp_PausingMerger when isMajorSocketOnRelayListenSide = true ->
            state <- PipeState.ThrottlingUp_ConnectingAll
            for i = 0 to (throttleUpSize-2) do
                pipeManager.needAConnection(this)
            ()
        | PipeState.ThrottlingUp_PausingMerger when isMajorSocketOnRelayListenSide = false ->
            let currentSplitterCycle = splitterChain.Pause(fun ()->())
            splitterChain.AddToFutureMembers(currentSplitterCycle+throttleCycleDelay)
            let buf = Array.create 4 (new Byte())
            Array.blit (BitConverter.GetBytes(currentSplitterCycle+throttleCycleDelay)) 0 buf 0 4
            let socket = socketStore.GetLastMinorSet().[0]
            try
                ignore(socket.BeginSend(buf,0,buf.GetLength(0),SocketFlags.None,null,null))
                state <- PipeState.ThrottlingUp_ConnectingAll
            with 
            | :? SocketException -> socketStore.Close()
            | :? ObjectDisposedException -> socketStore.Close()
            
        
        | _ -> ()
        Monitor.Exit lockobj
    member private this.DataReceiveToMajorSocket(result: IAsyncResult)=
        printfn "stub"
        

    member private this.DataSendToMajorSocket(result: IAsyncResult) =
        printfn "stub"

    member private this.DataReceiveToMinorSocket(result: IAsyncResult)=
        Monitor.Enter lockobj
        match state with
        | PipeState.AcceptingConnections when isMajorSocketOnRelayListenSide=false->
            let h = result.AsyncState :?> (Socket*byte[])
            let socket =  fst(h)
            let socketIndex = snd(h)
            try
                let received = socket.EndReceive(result)
                if received <> 1 then
                    socketStore.Close()
                else
                    socketStore.AddMinorSocket(socket,((int)(socketIndex.[0])))
                    if socketStore.ConnectedSockets = socketStore.GetLastMinorSet().GetLength(0) then // we have received enough connections, now try to connect 
                        state <- PipeState.Connecting
                        pipeManager.needAConnection(this)
            with 
            | :? SocketException -> socketStore.Close()
            | :? ObjectDisposedException -> socketStore.Close()
            ()
        
        | PipeState.ThrottlingUp_ReadingSyncInfo when isMajorSocketOnRelayListenSide = false -> 
            let h = result.AsyncState :?> (Socket*byte[])
            let socket = fst(h)
            let pa = snd(h)
            try
                let readCount = socket.EndReceive(result)
                if readCount <> pa.GetLength(0) then
                    socketStore.Close()
                else
                    throttleUpSize <- (int) pa.[1]
                    socketStore.AddMinorSet(throttleUpSize)
                    socketStore.AddMinorSocket(socket,(int)pa.[0])
                    let targetMergerCycleNumber = BitConverter.ToInt32(pa,2)
                
                    mergerChain.AddToFutureMembers(targetMergerCycleNumber)
                    let currentSplitterCycle = splitterChain.Pause(fun ()->())
                    splitterChain.AddToFutureMembers(currentSplitterCycle+throttleCycleDelay)
                    let buf = Array.create 4 (new Byte())
                    Array.blit (BitConverter.GetBytes(currentSplitterCycle+throttleCycleDelay)) 0 buf 0 4
                    let socket = socketStore.GetLastMinorSet().[0]
                    ignore(socket.BeginSend(buf,0,buf.GetLength(0),SocketFlags.None,null,null))
                    state <- PipeState.ThrottlingUp_ConnectingAll
            with 
            | :? SocketException -> socketStore.Close()
            | :? ObjectDisposedException -> socketStore.Close()
            ()
        | PipeState.ThrottlingUp_ReadingSyncInfo when isMajorSocketOnRelayListenSide = true ->
            let h = result.AsyncState :?> (Socket*byte[])
            let socket = fst(h)
            let pa = snd(h)
            try
                let readCount = socket.EndReceive(result)
                if readCount <> pa.GetLength(0) then
                    socketStore.Close()
                else
                    let targetMergerCycleNumber = BitConverter.ToInt32(pa,0)
                    mergerChain.AddToFutureMembers(targetMergerCycleNumber)
                    state <- PipeState.ThrottlingUp_ConnectingAll
                    for i = 0 to (throttleUpSize-2) do
                        pipeManager.needAConnection(this)
            with 
            | :? SocketException -> socketStore.Close()
            | :? ObjectDisposedException -> socketStore.Close()
            ()
               
        
        | PipeState.ThrottlingUp_ConnectingAll when isMajorSocketOnRelayListenSide = false ->
            let h = result.AsyncState :?> (Socket*byte[])
            let socket =  fst(h)
            let socketIndex = snd(h)
            try
                let received = socket.EndReceive(result)
                if received <> 1 then
                    socketStore.Close()
                else
                    socketStore.AddMinorSocket(socket,((int)(socketIndex.[0])))
                    if socketStore.ConnectedSockets = socketStore.GetLastMinorSet().GetLength(0) then // we have received enough connections, now try to connect 
                        state <- PipeState.Relaying
                        let s = new StreamSplitter(socketStore,socketStore.MajorSocket,socketStore.GetLastMinorSet(),pipeManager.getDynamicSegmentSize(),pipeManager.getDynamicSocketBufferSize())
                        let m = new StreamMerger(socketStore,socketStore.MajorSocket,socketStore.GetLastMinorSet(),pipeManager.getDynamicSegmentSize(),pipeManager.getDynamicSocketBufferSize())
                        splitterChain.AddToFutureMembers(s)
                        mergerChain.AddToFutureMembers(m) 
                        splitterChain.Resume()
                        mergerChain.Resume()
            with 
            | :? SocketException -> socketStore.Close()
            | :? ObjectDisposedException -> socketStore.Close()
            ()
        | _ -> ()
        Monitor.Exit lockobj
    member private this.DataSendToMinorSocket(result: IAsyncResult)=
        printfn "implement"
            
    
    member private this.ReadSocketIndex(socket:Socket)=
        Monitor.Enter lockobj
        let index = Array.create 1 (new Byte())
        try
            ignore(socket.BeginReceive(index,0,index.GetLength(0),SocketFlags.None,minorReadCallback,(socket,index)))
        with 
            | :? SocketException -> socketStore.Close()
            | :? ObjectDisposedException -> socketStore.Close()
        Monitor.Exit lockobj

    member private this.InitRelay()=
        Monitor.Enter lockobj
        reachedRelay <- (reachedRelay+1)
        pipeManager.dataTransferIsAboutToBegin(this)
        state <- PipeState.Relaying
        let s = new StreamSplitter(socketStore,socketStore.MajorSocket,socketStore.GetLastMinorSet(),pipeManager.getSegmentSize(),pipeManager.getMinorSocketBufferSize())
        let m = new StreamMerger(socketStore,socketStore.MajorSocket,socketStore.GetLastMinorSet(),pipeManager.getSegmentSize(),pipeManager.getMinorSocketBufferSize())
        splitterChain.AddToChain(s)
        mergerChain.AddToChain(m)
        splitterChain.UpdateChain()
        mergerChain.UpdateChain()
        splitterChain.Resume()
        mergerChain.Resume()
        Monitor.Exit lockobj
    member this.Close()=
        for sock in pendingSockets do
            sock.Close() 
        socketStore.Close()
    member this.AddPendingSocket(s: Socket)=
        pendingSockets.Add(s)
    

    interface IDataPipe with
        member x.TotalTransferedData()=
            let ch = chainAggregateData(splitterChain.GetAll())+chainAggregateData(mergerChain.GetAll())
            ch