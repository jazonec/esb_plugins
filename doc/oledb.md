## Плагин, реализующий взаимодействие с произвольным OleDB-источником

### Настройки
Настройки реализованы через json-объект, схемы находятся в архиве.  
  
Для водящей точки описывается массив заданий, срабатывающих по расписанию. Указывается sql-команда, класс и тип входящего сообщения. В случае, когда необходимо выполнить sql-команду после отправки сообщения в шину, необходимо заполнить соответствующие настройки постобработки.  
  
Для исходящей точки реализована возможность обработки сообщения, текст которого соответствует json-схеме исходящей команды.  

### Примеры настроек
  
***Входящая точка:***  
  
```json
{
    "$schema": "../../odbc-ingoing-connection-point-settings-schema.json",

	"Debug": {
		"DebugMode": true
	},
	"Connection": {
		"ConnectionString": "Provider=ASAProv.90;DRIVER = {Adaptive Server Anywhere 9.0};ENG=db_server;DBN=db_base;UID=user;PWD=password;LINKS=TCPIP{HOST=db_server;DoBroadcast=Direct}"
	},
	"Commands": [
		{
			"Name": "buffer",
			"MessageType": "inbox_buffer",
			"ExecuteAfterSend": true,
			"SendEachRow": true,
			"Cron": "0 0/5 * * * ?",
			"SQL": "SELECT message_id, message_type, message_class, message_text FROM esb_buffer WHERE state = 0",
			"CommandAfterSend": {
				"Name": "delete_buffer",
				"SQL": "UPDATE esb_buffer set state = 1 WHERE message_id = ?",
				"Parameters": [
					{
						"Name": "message_id",
						"ParameterType": 1,
						"JsonPath": "message_id"
					}
				]
			}
		},
		{
			"Name":"temperature",
			"MessageClassId": "7004",
			"MessageType": "DTP",
			"ExecuteAfterSend":false,
			"SendEachRow":false,
			"Cron":"0 0 * * * ?",
			"SQL":"SELECT top 10 id, date_time, value tab_temperature order by date_time desc"
		}
	]
}
```
  
***Исходящее сообщение-команда:***  
  
```json
{
	"CommandType": "ExecuteNonQuery",
	"CreateResponse": "true",
	"ResponseMessageType": "OleDbResponse",
	"SqlCommand": "INSERT INTO TEST_01 (STRING_DATA, NUMERIC_DATA, DATE_DATA, DECIMAL_DATA, INT_DATA) VALUES( :1, :2, :3, :4, :5)",
	"Parameters": [
		{
			"Name": "StringParameter",
			"ParameterType": 3,
			"ValueString": "Test point 3"
		},
		{
			"Name": "FloatParameter",
			"ParameterType": 4,
			"ValueFloat": "15.35"
		},
		{
			"Name": "DataParameter",
			"ParameterType": 0,
			"ValueDateTime": "2022-11-29 00:00:00"
		},
		{
			"Name": "DecimalParameter",
			"ParameterType": 4,
			"ValueFloat": "123456890123456789012345.355"
		},
		{
			"Name": "IntParameter",
			"ParameterType": 2,
			"ValueInt": 723456
		}
	]
}
```