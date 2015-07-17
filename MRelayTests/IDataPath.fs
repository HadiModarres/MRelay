module IDataPath

open System
open System.Net
open System.Net.Sockets

[<AllowNullLiteral>]
type IDataPath= 
    abstract entranceIP: unit->IPAddress
    abstract entrancePort: unit->int
    abstract exitIP: unit->IPAddress
    abstract exitPort: unit->int



