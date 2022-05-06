## Формирование массива сообщений
Плагин формирует batch-сообщение, выбирая из очереди на отправку все имеющиеся сообщения.
Сообщения группируются по type
Формат генерируемого сообщения
```xml
<BatchMessage>
  <MessagesQty>N</MessagesQty>
  <Types>
    <type_01>
      <messages>
        <message originalId="хххххххх-хххх-хххх-хххх-хххххххххххх" originalClassId="1">
        </message>
      </messages>
    </type_01>
    <type_02>
      <messages>
        <message originalId="хххххххх-хххх-хххх-хххх-хххххххххххх" originalClassId="2">
        </message>
      </messages>
    </type_02>
  </Types>
</BatchMessage>
```
