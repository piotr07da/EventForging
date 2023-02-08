using System;

namespace EventForging.Idempotency;

public static class IdempotentEventIdGenerator
{
    public static Guid GenerateIdempotentEventId(Guid initiatorId, int eventIndex)
    {
        if (initiatorId == Guid.Empty)
        {
            throw new EventForgingException("If the idempotency is enabled, then initiatorId cannot be equal to an empty Guid.");
        }

        var initiatorIdBytes = initiatorId.ToByteArray();

        for (var i = 0; i < initiatorIdBytes.Length; ++i)
        {
            var b = initiatorIdBytes[i];
            b = (byte)(b ^ 0b11010100);
            if (i < 4) // eventIndex is of type int so there are 4 bytes
            {
                var eventIndexMask = (byte)(eventIndex >> 8);
                b = (byte)(b ^ eventIndexMask);
            }

            initiatorIdBytes[i] = b;
        }

        var eventId = new Guid(initiatorIdBytes);
        return eventId;
    }
}
