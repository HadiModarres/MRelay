module MonitorTest

open System
open Microsoft.VisualStudio.TestTools.UnitTesting
open System.Security.Cryptography
open System.Resources
open System.IO
open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Collections
open SocketStore
open Relay
open RelayMonitor
open IDataPipe
open IMonitorDelegate
open TestTypes



[<TestClass>]
type MonitorTest() as this =   
    let mutable delFired = false
    let change1 = new Generic.Queue<int>()
    let mutable dr = null
   // let mutable ar = null
    do 
        change1.Enqueue(3000)
        change1.Enqueue(1*1024*1024)
        change1.Enqueue(1*1024*1024)
        change1.Enqueue(1*1024*1024)
        change1.Enqueue(1*1024*1024)
        change1.Enqueue(1*1024*1024)
        change1.Enqueue(1*1024*1024)
        change1.Enqueue(1*1024*1024)
        this.init()
        this.TestRef()

    member this.init()=
        let ar = Array.create 50000000 0uy
        dr <- new WeakReference(ar)
    member this.TestRef()=
        GC.Collect()
        
        printfn "is alive: %b" dr.IsAlive
        ()
    [<TestMethod>]
    member x.TestStreamDetection()=
       // dr <- new WeakReference(ar)
        let m = new Monitor(this,1000)
        let t = new t1(change1)
        m.Add(t)
        m.Start()
        Thread.Sleep(10000)
        Assert.AreEqual(delFired,true)

    interface IMonitorDelegate with
        member x.objectHasReachedActivityCriteria(obje: obj)=
            if delFired then
                delFired <- false
            else 
                delFired <- true
            
        
