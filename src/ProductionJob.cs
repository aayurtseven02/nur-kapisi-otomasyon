using System.Text.Json.Serialization;
namespace AnkaStudio;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductionJobPhase { Draft, Preparing, TextReview, Planning, Producing, Uploading, Publishing, Completed, Stopped, RetryWaiting, Failed }

// Job policy and execution state live together; screen controls are not a job queue.
public sealed class ProductionJob
{
    public string Mode {get;set;}="guided";
    public string PublishMode {get;set;}="";
    public string Authorization {get;set;}="";
    public string Channel {get;set;}="";
    public bool Pending {get;set;}
    public bool OwnMediaConsent {get;set;}
    public int Repairs {get;set;}
    public ProductionJobPhase Phase {get;set;}=ProductionJobPhase.Draft;
    public DateTimeOffset UpdatedAt {get;set;}=DateTimeOffset.UtcNow;
    public string ExecutionTarget {get;set;}="local";
    public string RemoteId {get;set;}="";
    public void Mark(ProductionJobPhase phase){Phase=phase;UpdatedAt=DateTimeOffset.UtcNow;}
}
