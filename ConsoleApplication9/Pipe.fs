module Pipe

open System
open SocketStore
open PipeStates
open System.Net.Sockets
open IPipeManager

type Pipe(pipeManager: IPipeManager,minorCount: int,isMajorSocketOnRelayListenSide: bool)as this=  // saves state info for each tcp pipe and implements MRelay protocol
    let mutable state = PipeState.AcceptingConnections
    let majorReadCallback = new AsyncCallback(this.DataReceiveToMajorSocket)
    let majorSendCallback = new AsyncCallback(this.DataSendToMajorSocket)
    let minorReadCallback = new AsyncCallback(this.DataReceiveToMinorSocket)
    let minorSendCallback = new AsyncCallback(this.DataSendToMinorSocket)
    let socketStore = new SocketStore(minorCount)


    member this.NewSocketReceived(socket: Socket)= 
        printfn "implement"
        match state with
        | PipeState.AcceptingConnections when isMajorSocketOnRelayListenSide=true -> state <- PipeState.Connecting; socketStore.MajorSocket <- socket; pipeManager.needAConnection(this) 
        | PipeState.AcceptingConnections when isMajorSocketOnRelayListenSide=false -> this.ReadSocketIndex(socket)
        
    member this.SocketConnected(socket: Socket)=
        printfn "implement"
    
    member this.DataReceiveToMajorSocket(result: IAsyncResult)=
        printfn "implement"

    member this.DataSendToMajorSocket(result: IAsyncResult) =
        printfn "implement"

    member this.DataReceiveToMinorSocket(result: IAsyncResult)=
        printfn "implement"

    member this.DataSendToMinorSocket(result: IAsyncResult)=
        printfn "implement"

    
    member this.ReadSocketIndex(socket:Socket)=
        let index = Array.create 1 (new Byte())
        ignore(socket.BeginReceive(index,0,1,SocketFlags.None,minorReadCallback,(socket,index)))
        