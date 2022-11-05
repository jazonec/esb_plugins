## Плагин для взаимодействия с брокером ActiveMQ

### Настройки
Настройки реализованы через json-объект. Схемы для формирования json размещаются внутри zip-архива плагина.
Для подключения к брокеру используется формат подключения "activemq:tcp://localhost:61616"

### Алгоритм
#### Входящая точка
* Создается подключение к брокеру
* Для каждой очереди из массива создается консюмер
* Появляющиеся в очереди сообщения забираются в шину
1. Тип сообщения AMQ_message
2. Заполняются стандартные NMS-свойства (NMSType, NMSTimeToLive, NMSMessageId, NMSTimestamp, NMSRedelivered, NMSPriority, NMSDestination, NMSDeliveryMode, NMSCorrelationID)
3. все заголовки сообщения переносятся в свойства, создаются свойства с префиксом "prop_"

#### Исходящая точка
* Создается подключение к брокеру
* Создается один продюсер
* Появляющиеся в очереди сообщения передаются в ActiveMQ
1. Тип сообщения переносится в NMSType
2. Создаются заголовки, куда переносятся одноименные свойства (ESB_Id, ESB_ClassId, ESB_Source, ESB_CorrelationId, ESB_CreationTime)
3. Все свойства сообщения переносятся в заголовки