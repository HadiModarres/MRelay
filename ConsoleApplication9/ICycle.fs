module ICycle

open System.Net.Sockets
open System

[<AllowNullLiteral>]
type ICycle= 
    abstract CycleCallback: (unit -> unit) with get,set
    abstract NoMoreCyclesCallback :(ICycle -> unit) with set
    abstract Cycle: unit -> unit
