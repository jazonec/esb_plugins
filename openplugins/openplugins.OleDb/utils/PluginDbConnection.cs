using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data;
using System.Data.OleDb;
using System.Data.Odbc;

namespace openplugins.OleDb
{
    internal class PluginDbConnection
    {
        private readonly ConnectionType connectionType;
        private readonly string connectionString;
        private readonly ConnectionSettings connectionSettings;
        public PluginDbConnection(ConnectionSettings settings)
        {
            connectionSettings = settings;
            connectionString = connectionSettings.ConnectionString;

            string _cs = connectionSettings.ConnectionString.ToUpper();
            if (_cs.Contains("PROVIDER"))
            {
                connectionType = ConnectionType.oledb;
            }
            else if (_cs.Contains("DSN"))
            {
                connectionType = ConnectionType.odbc;
            }
            else
            {
                throw new ArgumentException("Неизвестный тип подключения", "ConnectionString");
            }
            if (settings.UsePool)
            {
                connectionString = connectionString + ";Pooling=true;Max Pool Size=" + settings.PoolSize;
            }
        }

        public void CheckConnection()
        {
            using (DbConnection con = GetConnection())
            {
                con.Open();
                con.Close();
            }
        }

        // *************************************************************
        // *** Универсальные коллекции
        public DbConnection GetConnection()
        {
            if (connectionType == ConnectionType.oledb)
                return GetOledbConnection();
            else if (connectionType == ConnectionType.odbc)
                return GetOdbcConnection();
            else
                return null;
        }
        public DbCommand GetCommand()
        {
            if (connectionType == ConnectionType.oledb)
                return GetOledbCommand();
            else if (connectionType == ConnectionType.odbc)
                return GetOdbcCommand();
            else
                return null;
        }
        public DbParameter GetParameter()
        {
            if (connectionType == ConnectionType.oledb)
                return GetOledbParameter();
            else if (connectionType == ConnectionType.odbc)
                return GetOdbcParameter();
            else
                return null;
        }
        public DbDataAdapter GetDataAdapter()
        {
            if (connectionType == ConnectionType.oledb)
                return GetOleDbDataAdapter();
            else if (connectionType == ConnectionType.odbc)
                return GetOdbcDataAdapter();
            else
                return null;
        }
        // ***
        // *************************************************************

        // *************************************************************
        // *** Выборка/изменение данных
        public DataTable GetDataTable(string commandText)
        {
            using (DbConnection connection = GetConnection())
            using (DbCommand command = GetCommand())
            {
                connection.Open();
                command.Connection = connection;
                command.CommandText = commandText;

                DbDataAdapter adapter = GetDataAdapter();
                adapter.SelectCommand = command;
                DataTable dt = new DataTable();
                adapter.Fill(dt);
                return dt;
            }
        }
        public DataTable GetDataTable(CommandSettings settings, List<CommandParameterWithData> parameters)
        {
            using (DbConnection connection = GetConnection())
            using (DbCommand command = GetCommand())
            {
                connection.Open();
                command.Connection = connection;
                command.CommandText = settings.SQL;
                AddParameters(command, parameters);

                DbDataAdapter adapter = GetDataAdapter();
                adapter.SelectCommand = command;
                DataTable dt = new DataTable();
                adapter.Fill(dt);
                return dt;
            }
        }
        public int ExecuteNonQuery(string commandText)
        {
            using (DbConnection connection = GetConnection())
            using (DbCommand command = GetCommand())
            {
                connection.Open();
                command.Connection = connection;
                command.CommandText = commandText;
                command.CommandType = CommandType.Text;
                return command.ExecuteNonQuery();
            }
        }
        public int ExecuteNonQuery(CommandSettings settings, List<CommandParameterWithData> parameters)
        {
            using (DbConnection connection = GetConnection())
            using (DbCommand command = GetCommand())
            {
                connection.Open();
                command.Connection = connection;
                command.CommandText = settings.SQL;
                AddParameters(command, parameters);
                return command.ExecuteNonQuery();
            }
        }
        public DbDataReader ExecuteQuery(string commandText)
        {
            using (DbConnection connection = GetConnection())
            using (DbCommand command = GetCommand())
            {
                connection.Open();
                command.Connection = connection;
                command.CommandText = commandText;
                command.CommandType = CommandType.Text;
                return command.ExecuteReader();
            }
        }
        // ***
        // *************************************************************

        private void AddParameters(DbCommand command, List<CommandParameterWithData> parameters)
        {
            foreach (CommandParameterWithData parameter in parameters)
            {
                DbParameter dbParameter = GetParameter();
                switch (parameter.ParameterType)
                {
                    case ParameterType.DateTime:
                        dbParameter.Value = parameter.ValueDateTime;
                        dbParameter.DbType = DbType.DateTime;
                        break;
                    case ParameterType.Int:
                        dbParameter.Value = parameter.ValueInt;
                        dbParameter.DbType = DbType.Int32;
                        break;
                    case ParameterType.String:
                        dbParameter.Value = parameter.ValueString;
                        dbParameter.DbType = DbType.String;
                        break;
                    case ParameterType.Float:
                        dbParameter.Value = parameter.ValueFloat;
                        dbParameter.DbType = DbType.Double;
                        break;
                    default:
                        throw new NotSupportedException("Тип параметра не поддерживается: " + (int)parameter.ParameterType);
                }
                command.Parameters.Add(dbParameter);
            }
        }
        private OdbcConnection GetOdbcConnection()
        {
            return new OdbcConnection(connectionString);
        }
        private OleDbConnection GetOledbConnection()
        {
            return new OleDbConnection(connectionString);
        }
        private OdbcCommand GetOdbcCommand()
        {
            return new OdbcCommand();
        }
        private OleDbCommand GetOledbCommand()
        {
            return new OleDbCommand();
        }
        private OdbcDataAdapter GetOdbcDataAdapter()
        {
            return new OdbcDataAdapter();
        }
        private OleDbDataAdapter GetOleDbDataAdapter()
        {
            return new OleDbDataAdapter();
        }
        private OdbcParameter GetOdbcParameter()
        {
            return new OdbcParameter();
        }
        private OleDbParameter GetOledbParameter()
        {
            return new OleDbParameter();
        }

        internal enum ConnectionType
        {
            odbc,
            oledb
        }
    }
}
