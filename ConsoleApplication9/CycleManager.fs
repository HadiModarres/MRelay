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
//    let mutable pauseAtCallback =
//        fun() -> ()

//    let mutable pausePending = false
//    let mutable pauseAtCycle = -1
    let mutable paused = true

    let toBeAttached = new Generic.Queue<ICycle>()
    let toBeAttachedCycleNumbers = new Generic.Queue<int>()

    let lockobj = new obj()




    member x.UpdateNeeded
        with set(update: bool)= updateNeeded <- true
    member x.CycleNum
        with get() = cycleNumber

    member x.GetAll
        with get() = chain

    
    
    member x.UpdateChain()=
        if chain.Count > 1 then
            for i=0 to (chain.Count-2) do
                chain.[i].CycleCallback <- chain.[i+1].Cycle
        if chain.Count > 0 then
            chain.[chain.Count-1].CycleCallback <- x.AfterCycle
        else
            chainCallback <- fun()->()
        updateNeeded <- false
        
    member x.AddToChain(cycler: ICycle)=
        ignore(chain.Add(cycler))
        cycler.NoMoreCyclesCallback <- x.NoMoreCyclesLeft
        printfn "adding to chain at %i" cycleNumber
 
    member x.NoMoreCyclesLeft(cycler: ICycle)=
        ignore(chain.Remove(cycler))
        updateNeeded <- true

    
    member private x.AfterCycle()=
        
        Monitor.Enter lockobj
        paused <- true
        cycleNumber <- (cycleNumber + 1)

//        printfn "cycle number: %i" cycleNumber
//        if pausePending && cycleNumber=pauseAtCycle then
//            chainCallback <- pauseAtCallback
//            pausePending <- false
//            pauseAtCycle <- -1
        if toBeAttached.Count>0 then
            if cycleNumber = toBeAttachedCycleNumbers.Peek() then
                x.AddToChain(toBeAttached.Dequeue())
                ignore(toBeAttachedCycleNumbers.Dequeue())
                updateNeeded <- true
        
        if updateNeeded= true then
                x.UpdateChain()

        chainCallback()
        Monitor.Exit lockobj

    member private x.BeforeCycle()=
        Monitor.Enter lockobj
        paused <- false
        chain.[0].Cycle()
        Monitor.Exit lockobj
    
    member x.Pause(cback: unit->unit) =
        Monitor.Enter lockobj
        chainCallback <- cback
        Monitor.Exit lockobj    
        cycleNumber
        
        
//    member x.PauseAt(cback: unit->unit,cycleNum: int)=
//        Monitor.Enter lockobj
//        if cycleNum = cycleNumber then // we have already reached the cycle
//            cback()
//        else if cycleNumber > cycleNum then // we have passed the requested cycle number already
//            ()
//        else    // should set to pause at the requested cycle number
//            pauseAtCycle <- cycleNum
//            pausePending <- true
//            pauseAtCallback <- cback
//        Monitor.Exit lockobj

    member x.Resume()=
        Monitor.Enter lockobj
        if chain.Count > 0 then  
            chainCallback <- x.BeforeCycle
            
            if paused=true then    
                x.BeforeCycle()
        else
            printfn "can't resume, chain empty"
        Monitor.Exit lockobj

    member x.AttachAfterCycle(cycler: ICycle,cycleNum: int)=
        toBeAttached.Enqueue(cycler)
        toBeAttachedCycleNumbers.Enqueue(cycleNum)
    member x.AddToFutureMembers(cycler: ICycle)=
        toBeAttached.Enqueue(cycler)
    member x.AddToFutureMembers(cycleNum: int)=
        toBeAttachedCycleNumbers.Enqueue(cycleNum)
