module RelayMonitor  // monitors relay's traffic on all the tcp connections and informs the relay when a certain criteria happens. Being implemented for dynamic tcp count grow

open System
open IDataPipe
open IMonitorDelegate
open System.Collections.Generic
open System.Collections
open System.Threading

type CriterionType=
| ConstantActivity
| TotalTransferExceeds

type Criterion(ac: CriterionType,value: 'T)=
    member x.CriterionType
        with get() = ac
    member x.Value
        with get() = value

type MonitorObject(p:IDataPipe)=
    let mutable totalTransferOnLastCycle = 0UL
    let mutable longestActiveTransfer = 0 
    let mutable currentSpeed = (float)0
    let lockobj = new obj()

    member x.TotalDataOnLast 
        with get() = totalTransferOnLastCycle
        
    member x.LongestActiveStreak 
        with get() = longestActiveTransfer

    member x.DataPipe 
        with get() = p
    member x.CurrentSpeed
        with get() = currentSpeed
        
    member x.Update(highestSpeed: float,intervalMillis: int)=
        Monitor.Enter lockobj
        let k = p.TotalTransferedData()
        currentSpeed <- ((float) (k - totalTransferOnLastCycle)) / (float)intervalMillis
     //   printfn "current speed: %f kilo bytes per second, interval millis: %i, total transfered: %i, last cycle transfered: %i" currentSpeed intervalMillis (p.TotalTransferedData()) totalTransferOnLastCycle 

        if (currentSpeed > (0.4 * (float)highestSpeed)) then
            longestActiveTransfer <- (longestActiveTransfer + 1)
        else 
            longestActiveTransfer <- 0

        totalTransferOnLastCycle <- k
        Monitor.Exit lockobj
    member x.Reset()=
        totalTransferOnLastCycle <- 0UL
        longestActiveTransfer <- 0

[<AllowNullLiteral>]
type Monitor(deleg: IMonitorDelegate,period: int) as x =
    let criteria = new Generic.List<Criterion>() // in order for monitor to fire the delegate method, it needs a member that is being monitored to reach all criteria contained in this list
    let monitoredObjects =new Generic.List<MonitorObject>()
    
    let mutable highestSpeedSoFar = (float)0
    let mutable processCount = 0


    let timerCallback = new TimerCallback(x.Process)
    let timer = new Threading.Timer(timerCallback)

    let lockobj = new obj()
    
    do  

        criteria.Add(new Criterion(CriterionType.ConstantActivity,6000))  // for a member to match this criteria it must have had a constant activity for at least 8 seconds
        criteria.Add(new Criterion(CriterionType.TotalTransferExceeds,3*1024*1024)) // for a member to match this criteria it must have moved at least 3MB of data
        
                                                                                    // if the two criteria above hold, we can almost be sure that the pipe is a heavy load pipe and should be throttled up, 
                                                                                    // average web page size according to statistics is apparently 1700 KB 

    member x.Add(objectToBeMonitored: IDataPipe)=
        Monitor.Enter lockobj
        monitoredObjects.Add(new MonitorObject(objectToBeMonitored))
        Monitor.Exit lockobj
    member x.Remove(objectToBeRemovedFromBeingMonitored: IDataPipe)=
        Monitor.Enter lockobj
        printfn "monitor remove called"
        if monitoredObjects.Count>0 then
            for i= monitoredObjects.Count-1 downto 0 do
                if monitoredObjects.[i].DataPipe = objectToBeRemovedFromBeingMonitored then
                    monitoredObjects.RemoveAt(i)
        Monitor.Exit lockobj
        
    member x.Start() = // monitor will stop by default after firing delegate method
        ignore(timer.Change(0,period))
    member x.Reset() =
        
        ignore(timer.Change(Timeout.Infinite,Timeout.Infinite))
        for o in monitoredObjects do
            o.Reset()
        processCount <- 0
        highestSpeedSoFar <- (float)0
            
    member x.AddCriteria(crit: CriterionType,value: 'T)=
        criteria.Add((new Criterion(crit,value)))
        

    member x.RemoveCriteriaTypeFromList(crit: CriterionType) =
        if criteria.Count>0 then
            for i=criteria.Count-1 downto 0 do
                if criteria.[i].CriterionType = crit then
                    criteria.RemoveAt(i)
        
    
    member private x.Update()=
        for o in monitoredObjects do
            o.Update(highestSpeedSoFar,period)
            if o.CurrentSpeed > highestSpeedSoFar then
                highestSpeedSoFar <- o.CurrentSpeed

    member private x.Process(timerObj: obj)=
        Monitor.Enter lockobj
        x.Update()
        
        if monitoredObjects.Count>0 then
            for i= monitoredObjects.Count-1 downto 0 do
                let f1 = x.MatchesCriterion monitoredObjects.[i]
                let pr = new Predicate<Criterion>(f1)
                if Array.TrueForAll(criteria.ToArray(),pr) then
                    printfn "firing"
                    deleg.objectHasReachedActivityCriteria(monitoredObjects.[i].DataPipe)
                    monitoredObjects.RemoveAt(i)

        processCount <- (processCount + 1)
        Monitor.Exit lockobj
    
    member private x.MatchesCriterion  (toBeTested: MonitorObject) (crit:Criterion)=
        match crit.CriterionType with
        | CriterionType.ConstantActivity ->
            if (toBeTested.LongestActiveStreak * period) >= (int)crit.Value then
                true
            else
                false
            
        | CriterionType.TotalTransferExceeds ->
            if toBeTested.DataPipe.TotalTransferedData() > (uint64)crit.Value then
                true
            else 
                false
        


