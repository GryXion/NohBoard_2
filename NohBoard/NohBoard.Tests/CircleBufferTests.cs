/*
Copyright (C) 2026 by NohBoard contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 2 of the License, or
(at your option) any later version.
*/

namespace ThoNohT.NohBoard.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using ThoNohT.NohBoard.Hooking;
    using Xunit;

    public sealed class CircleBufferTests
    {
        [Fact]
        public void Constructor_PreFillsWithDefaultElement()
        {
            var buffer = new CircleBuffer<int>(size: 4, defaultElem: 7);
            buffer.ToArray().Should().BeEquivalentTo(new[] { 7, 7, 7, 7 });
            buffer.Size.Should().Be(4);
        }

        [Fact]
        public void Add_NeverGrowsBeyondSize()
        {
            var buffer = new CircleBuffer<int>(size: 3, defaultElem: 0);
            for (var i = 1; i <= 100; i++) buffer.Add(i);
            buffer.ToArray().Should().HaveCount(3);
        }

        [Fact]
        public void Add_EvictsOldestFirst()
        {
            var buffer = new CircleBuffer<int>(size: 3, defaultElem: 0);
            buffer.Add(1);
            buffer.Add(2);
            buffer.Add(3);
            buffer.Add(4); // 1 is evicted

            buffer.ToArray().Should().Equal(2, 3, 4);
        }

        [Fact]
        public async Task ConcurrentAdds_AreThreadSafe()
        {
            var buffer = new CircleBuffer<int>(size: 16, defaultElem: 0);

            var tasks = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() =>
                {
                    for (var i = 0; i < 10_000; i++) buffer.Add(i);
                }))
                .ToArray();

            await Task.WhenAll(tasks);
            buffer.ToArray().Should().HaveCount(16);
        }
    }
}
