using System;

namespace openplugins.ActiveMQ
{
    internal interface IEsbAmqManager
    {
        bool HasError { get; }
        string ErrorMessage { get; }
        void WriteLogString(string logString);
        void SetError(string error);
    }
}