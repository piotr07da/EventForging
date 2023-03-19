// ReSharper disable InconsistentNaming

using EventForging.Serialization;
using Xunit;

namespace EventForging.Tests.Serialization
{
    public class DefaultEventTypeNameMapper_tests
    {
        [Fact]
        public void shall_return_type_by_its_full_name()
        {
            var sut = new DefaultEventTypeNameMapper(typeof(DefaultEventTypeNameMapper_tests).Assembly);
            var actualType = sut.TryGetType("EventForging.Tests.Serialization.DefaultEventTypeNameMapper_tests");
            Assert.True(actualType == typeof(DefaultEventTypeNameMapper_tests));
        }

        [Fact]
        public void shall_return_null_for_full_name_of_not_existing_type()
        {
            var sut = new DefaultEventTypeNameMapper(typeof(DefaultEventTypeNameMapper_tests).Assembly);
            var actualType = sut.TryGetType("EventForging.Tests.Serialization.NOT_EXISTING_TYPE");
            Assert.Null(actualType);
        }

        [Fact]
        public void shall_return_full_type_name()
        {
            var sut = new DefaultEventTypeNameMapper(typeof(DefaultEventTypeNameMapper_tests).Assembly);
            var actualName = sut.TryGetName(typeof(DefaultEventTypeNameMapper_tests));
            Assert.Equal("EventForging.Tests.Serialization.DefaultEventTypeNameMapper_tests", actualName);
        }

        [Fact]
        public void assemblies_cannot_be_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new DefaultEventTypeNameMapper(null);
            });
        }

        [Fact]
        public void assemblies_cannot_be_empty()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                new DefaultEventTypeNameMapper();
            });
        }
    }
}
