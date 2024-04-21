using System.Collections.Generic;
using System;
using Newtonsoft.Json;

namespace openplugins.OleDb
{
    internal class IngoingSettings
    {
        public DebugSettings Debug { set; get; }
        public ConnectionSettings Connection { set; get; }
        public List<IngoingCommandSettings> Commands { set; get; }
    }
    internal class OutgoingSettings
    {
        public DebugSettings Debug { set; get; }
        public ConnectionSettings Connection { set; get; }
        public List<OutgoingCommandSettings> Commands { set; get; }
    }
    internal class IngoingCommandSettings : CommandSettings
    {
        public IngoingCommandSettings()
        {
            Name = new Guid().ToString();
            MessageType = "RecordSet";
            MessageClassId = null;
            ExecuteAfterSend = false;
        }
        public string Cron { get; set; }
        public bool SendEachRow { set; get; }
        public bool ExecuteAfterSend { get; set; }
        public OutgoingCommandSettings CommandAfterSend { get; set; }
    }
    internal class OutgoingCommandSettings : CommandSettings
    {
        public OutgoingCommandSettings()
        {
            Name = new Guid().ToString();
            CreateResponse = false;
            MessageType = null;
            MessageClassId = null;
        }
        public List<Parameter> Parameters { set; get; }
        public bool CreateResponse { get; set; }
        public string ResponseMessageType { get; set; }
        public string ResponseMessageClassId { get; set; }
    }
    internal class ConnectionSettings
    {
        public ConnectionSettings()
        {
            UsePool = true;
            PoolSize = 10;
        }
        public string ConnectionString { set; get; }
        public bool UsePool { set; get; }
        public int PoolSize { set; get; }
    }
    internal class DebugSettings
    {
        public DebugSettings()
        {
            DebugMode = false;
            StartDelay = 20;
        }

        public bool DebugMode { set; get; }
        public int StartDelay { set; get; }
    }
    abstract class CommandSettings
    {
        public string Name { set; get; }
        public string SQL { get; set; }
        public PluginCommandType CommandType { set; get; }
        public string MessageType { get; set; }
        public string MessageClassId { get; set; }
    }
    internal class Parameter
    {
        public string Name { get; set; }
        public ParameterType ParameterType { get; set; }
        public string JsonPath { get; set; }
    }
    internal class MessageAsCommand : OutgoingCommandSettings
    {
        public new List<CommandParameterWithData> Parameters { get; set; }
    }
    internal class CommandParameterWithData
    {
        public string Name { get; set; }
        public ParameterType ParameterType { get; set; }
        [JsonConverter(typeof(CustomDateTimeConverter))]
        public DateTime ValueDateTime { get; set; }
        public int ValueInt { get; set; }
        public string ValueString { get; set; }
        public float ValueFloat { get; set; }
    }
    internal enum ParameterType
    {
        DateTime,
        Int,
        String,
        Float
    }
    enum PluginCommandType
    {
        NonQuery,
        Query
    }
}