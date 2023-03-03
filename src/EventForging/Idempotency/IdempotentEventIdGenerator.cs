namespace EventForging.Idempotency;

public static class IdempotentEventIdGenerator
{
    public static Guid GenerateIdempotentEventId(Guid initiatorId, long eventIndex)
    {
        if (initiatorId == Guid.Empty)
        {
            throw new EventForgingException("If the idempotency is enabled, then initiatorId cannot be equal to an empty Guid.");
        }

        var initiatorIdBytes = initiatorId.ToByteArray();

        var initiatorIdByteIndex = 0;
        var eventIndexByteIndex = 0;

        for (; initiatorIdByteIndex < initiatorIdBytes.Length;)
        {
            var eb = (byte)(initiatorIdBytes[initiatorIdByteIndex] ^ 0b11010100);
            var ob = (byte)(255 - initiatorIdBytes[initiatorIdByteIndex + 1]);

            var eventIndexByte = (byte)(eventIndex >> (eventIndexByteIndex * 8));
            eb = (byte)(eb ^ eventIndexByte);
            ob = (byte)(ob ^ eventIndexByte);

            initiatorIdBytes[initiatorIdByteIndex] = eb;
            initiatorIdBytes[initiatorIdByteIndex + 1] = ob;

            initiatorIdByteIndex += 2;
            eventIndexByteIndex += 1;
        }

        var eventId = new Guid(initiatorIdBytes);
        return eventId;
    }
}
