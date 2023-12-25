namespace openplugins.RabbitMQ
{
    internal interface IEsbRmqManager
    {
        bool HasError { get; }
        string ErrorMessage { get; }
        void WriteLogString(string logString);
        void SetError(string error);
    }
}