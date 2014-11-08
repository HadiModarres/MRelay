module RelayMonitor  // monitors relay's traffic on all the tcp connections and informs the relay when a certain criteria happens. Being implemented for dynamic tcp count grow

open System
open IDataPipe

type Monitor(delegateMethod: IDataPipe -> unit) =
    let a =1 
    do
        printfn "implement"
    member x.Add(objectToBeMonitored: IDataPipe)=
        printfn "implement"
    member x.Remove(objectToBeRemoveFromBeingMonitored: IDataPipe)=
        printfn "implement"
    member x.Start() = // monitor will stop by default after firing delegate method
        printfn "implement"
    member x.Reset() =
        printfn "implement"
   
    member x.RemoveAll()=
        printfn "implement"



