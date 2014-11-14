module ICycle

open System.Net.Sockets
open System

[<AllowNullLiteral>]
type ICycle= 
    abstract CycleCallback: (unit -> unit) with get,set
    abstract Cycle: unit -> unit
