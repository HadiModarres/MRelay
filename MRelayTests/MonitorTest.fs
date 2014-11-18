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
    do 
        change1.Enqueue(3000)
        change1.Enqueue(1*1024*1024)
        change1.Enqueue(1*1024*1024)
        change1.Enqueue(1*1024*1024)
        change1.Enqueue(1*1024*1024)
        change1.Enqueue(1*1024*1024)
        change1.Enqueue(1*1024*1024)
        change1.Enqueue(1*1024*1024)
    

    [<TestMethod>]
    member x.TestStreamDetection()=
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
            
        
