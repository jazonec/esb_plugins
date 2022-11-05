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

### ActiveMQ connector
Позволяет отправлять/получать сообщения в брокер ActiveMQ.
[Подробно](/doc/ActiveMQ.md)
