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


type t1(changes: Generic.Queue<int>)=
    let mutable total = 0
    interface IDataPipe with
        member x.TotalTransferedData()= // return the count of the bytes that have been passed through this object
            if changes.Count >0 then
                total <- total + changes.Dequeue()
            (uint64)total
[<TestClass>]
type MonitorTest() as this =   
    
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
        change1.Enqueue(1*1024*1024)
    

    [<TestMethod>]
    member x.TestStreamDetection()=
        let m = new Monitor(this,1000)
        let t = new t1(change1)
        m.Add(t)
        m.Start()

    interface IMonitorDelegate with
        [<TestMethod>]
        [<Timeout(9)>]
        member x.objectHasReachedActivityCriteria(obje: obj)=
            Assert.IsNotNull(obje)
            
        
