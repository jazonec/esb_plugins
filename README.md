# Datareon ESB openplugins
Подборка прагинов для Datareon ESB

Проекты собираются в debug-режиме (рекомендация производителя шины, описана в документации). Добавлен скрипт, осуществляющий после сборки подготовку zip-архива в требуемом формате.
Архивы размещаются в openplugins\plugins

Внимание! Для успешной сборки разместите ESB_ConnectionPoints.PluginsInterfaces.dll в openplugins\

### AESServerClient
Предоставляет функциональность, позволяющую обмениваться с внешним потребителем зашифрованными сообщениями.
[Подробно](/doc/aesserverclient.md)

### ReflectBatchMessage
Позволяет сгруппировать одиночные сообщения в одно пакетное.
[Подробно](/doc/reflectbatchmessage.md)

### OleDB
Предоставляет функциональность взаимодействия с произвольным OleDB источником.
[Подробно](/doc/oledb.md)

### ActiveMQ connector
Позволяет отправлять/получать сообщения в брокер ActiveMQ.
[Подробно](/doc/ActiveMQ.md)

### RabbitMQ connector
Позволяет отправлять/получать сообщения в брокер RabbitMQ.
[Подробно](/doc/RabbitMQ.md)

### ActiveDirectory connector
Позволяет получать/изменять объекты ActiveDirectory.
[Подробно](/doc/ActiveDirectory.md)

### reflectors connector
Позволяет обрабатывать сообщения и возвращать в шину результат обработки.
[Подробно](/doc/reflectors.md)

### multijob connector
Реализует пример формирования входящих сообщений по расписанию.
[Подробно](/doc/multijob.md)
