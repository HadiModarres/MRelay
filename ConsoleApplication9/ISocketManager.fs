module ISocketManager
open System.Net.Sockets
open System

[<AllowNullLiteral>]
type ISocketManager= 
    abstract MinorReadDone: set:Socket[] -> unit 
    abstract MajorReadDone: unit -> unit
    abstract SocketExceptionOccured: Socket -> Exception -> unit
    