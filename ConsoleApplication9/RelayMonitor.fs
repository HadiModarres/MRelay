// Copyright 2014 Hadi Modarres
// modarres.zadeh@gmail.com
//
// This file is part of MRelay.
// MRelay is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MRelay is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
module RelayMonitor  // monitors relay's traffic on all the tcp connections and informs the relay when a certain criteria happens. Being implemented for dynamic tcp count grow

open System
open IDataPipe
open IMonitorDelegate
open System.Collections.Generic
open System.Collections
open System.Threading
open System.Windows.Forms

type CriterionType=
| ConstantActivity
| TotalTransferExceeds
| ConstantTransferExceeds


type Criterion(ac: CriterionType,value: 'T)=
    member x.CriterionType
        with get() = ac
    member x.Value
        with get() = value

type MonitorObject(p:IDataPipe)=
    let mutable totalTransferOnLastCycle = 0UL
    let mutable longestActiveTransfer = 0 
    let mutable largestActiveTransfer = 0UL // this is how much data(in bytes) has been transfered actively
    let mutable currentSpeed = (float)0
    let speedHist = new List<float>()
    let lockobj = new obj()
    let mutable start = 0
   
    member x.Start
        with get() = start
        and set(s: int)= start<- s
    member x.TotalDataOnLast 
        with get() = totalTransferOnLastCycle
        
    member x.LongestActiveStreak 
        with get() = longestActiveTransfer

    member x.LargestActiveStreak
        with get() = largestActiveTransfer
    member x.DataPipe 
        with get() = p
    member x.CurrentSpeed
        with get() = currentSpeed
    
    member x.SpeedHistory
        with get() = speedHist.ToArray()
        
    member x.Update(highestSpeed: float,intervalMillis: int)=
        Monitor.Enter lockobj
        let k = p.TotalTransferedData()
        currentSpeed <- ((float) (k - totalTransferOnLastCycle)) / (float)intervalMillis
        speedHist.Add(currentSpeed)
        if (currentSpeed > (0.2 * (float)highestSpeed)) then
            longestActiveTransfer <- (longestActiveTransfer + 1)
            largestActiveTransfer <- largestActiveTransfer+(k-totalTransferOnLastCycle)
        else 
            longestActiveTransfer <- 0
            largestActiveTransfer <- 0UL

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

        criteria.Add(new Criterion(CriterionType.ConstantActivity,4000))  // for a member to match this criteria it must have had a constant activity for at least 4 seconds
//        criteria.Add(new Criterion(CriterionType.TotalTransferExceeds,1*1024*1024)) // for a member to match this criteria it must have moved at least 1MB of data
        criteria.Add(new Criterion(CriterionType.ConstantTransferExceeds,1024*1024)) // for a member to match this, it must move 1MB of data actively(without stop)
                                                                                       
                                                                                       //   so for a pipe to be considered a stream or download pipe it must remain active for at least
                                                                                       //   4 seconds and move 1MB actively, so for example for a very fast connection that loads website instantly,
                                                                                       //   it will transfer 1MB very quickly but will stop afterwards and doesn't reach the 4 seconds criterion. 
                                                                                       //   and for a slow connection that is loading websites slowly, it will remain active for long periods of time but will struggle
                                                                                       //   to reach the constant transfer of 1MB, so the combination of the two probably detects download and stream pipes well regardless of 
                                                                                       //   connection speed
                                                                                      
        
                                                                                     

    member x.Add(objectToBeMonitored: IDataPipe)=
        Monitor.Enter lockobj
        let om = new MonitorObject(objectToBeMonitored)
        om.Start <- processCount
        monitoredObjects.Add(om)
        
        Monitor.Exit lockobj
    member x.Remove(objectToBeRemovedFromBeingMonitored: IDataPipe)=
        Monitor.Enter lockobj
        if monitoredObjects.Count>0 then
            for i= monitoredObjects.Count-1 downto 0 do
                if monitoredObjects.[i].DataPipe = objectToBeRemovedFromBeingMonitored then
                    monitoredObjects.RemoveAt(i)
        Monitor.Exit lockobj
        
    member x.Start() = 
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
                        ()
//                    deleg.objectHasReachedActivityCriteria(monitoredObjects.[i].DataPipe)
//                    monitoredObjects.RemoveAt(i)
//                    highestSpeedSoFar <- 0.0

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
        | CriterionType.ConstantTransferExceeds ->
            if toBeTested.LargestActiveStreak > (uint64) crit.Value then
                true
            else
                false

   
    member x.GetSpeedValues()=
        monitoredObjects
