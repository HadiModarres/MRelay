module CycleManager

open System
open System.Collections
open Splitter
open Merger
open ICycle
open System.Threading
open System.Collections.Generic

type CycleManager() as x=
    let chain = new Generic.List<ICycle>()
    let mutable cycleNumber = 0
    let mutable updateNeeded = false
    let mutable chainCallback =
        fun() -> ()
    let mutable pauseAtCallback =
        fun() -> ()

    let mutable pausePending = false
    let mutable pauseAtCycle = -1

    let lockobj = new obj()

    member x.CycleNum
        with get() = cycleNumber

    member x.GetAll
        with get() = chain
    
    
    member private x.UpdateChain()=
        if chain.Count > 1 then
            for i=0 to (chain.Count-2) do
                let c1 = chain.[i]
                let c2 = chain.[i+1]
                c1.CycleCallback <- c2.Cycle
        if chain.Count > 0 then
            let c3 = chain.[chain.Count-1]
            c3.CycleCallback <- x.CheckCycle
        else
            chainCallback <- fun()->()
        updateNeeded <- false
        
    member x.AddToChain(cycler: ICycle)=
        ignore(chain.Add(cycler))
        cycler.NoMoreCyclesCallback <- x.NoMoreCyclesLeft
 
    member x.NoMoreCyclesLeft(cycler: ICycle)=
        ignore(chain.Remove(cycler))
        updateNeeded <- true

    
    member private x.CheckCycle()=
        Monitor.Enter lockobj
        cycleNumber <- (cycleNumber + 1)

        printfn "cycle number: %i" cycleNumber
        if pausePending && cycleNumber=pauseAtCycle then
            chainCallback <- pauseAtCallback
            pausePending <- false
            pauseAtCycle <- -1
        if updateNeeded = true then
            x.UpdateChain()
        chainCallback()
        Monitor.Exit lockobj

    member x.Pause(cback: unit->unit) =
        Monitor.Enter lockobj
        chainCallback <- cback
        Monitor.Exit lockobj    
    
    member x.PauseAt(cback: unit->unit,cycleNum: int)=
        Monitor.Enter lockobj
        if cycleNum = cycleNumber then // we have already reached the cycle
            cback()
        else if cycleNumber > cycleNum then // we have passed the requested cycle number already
            ()
        else    // should set to pause at the requested cycle number
            pauseAtCycle <- cycleNum
            pausePending <- true
            pauseAtCallback <- cback
        Monitor.Exit lockobj
    member x.Resume()=
        if chain.Count > 0 then
            let c = chain.[0] 
            chainCallback <- c.Cycle
            x.UpdateChain()
            c.Cycle()
        else
            printfn "can't resume, chain empty"
