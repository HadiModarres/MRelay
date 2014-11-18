module IMonitorDelegate

[<AllowNullLiteral>]
type IMonitorDelegate= 
    abstract objectHasReachedActivityCriteria: obj -> unit


