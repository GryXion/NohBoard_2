/*
Copyright (C) 2026 by NohBoard contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 2 of the License, or
(at your option) any later version.
*/

namespace ThoNohT.NohBoard.Tests
{
    using System.Drawing;
    using FluentAssertions;
    using ThoNohT.NohBoard.Extra;
    using Xunit;

    public sealed class GeomTests
    {
        [Fact]
        public void TPoint_TranslateByInt_ReturnsCorrectPoint()
        {
            var p = new TPoint(10, 20);
            var translated = p.Translate(5, -3);
            translated.X.Should().Be(15);
            translated.Y.Should().Be(17);
        }

        [Fact]
        public void TPoint_Subtraction_ReturnsSize()
        {
            var a = new TPoint(10, 20);
            var b = new TPoint(3, 4);
            var diff = a - b;
            diff.Should().Be(new Size(7, 16));
        }

        [Fact]
        public void TPoint_AdditionWithSize_TranslatesPoint()
        {
            var p = new TPoint(0, 0);
            var moved = p + new Size(10, 10);
            moved.X.Should().Be(10);
            moved.Y.Should().Be(10);
        }

        [Fact]
        public void TPoint_Clone_CreatesIndependentCopy()
        {
            var p = new TPoint(7, 8);
            var c = p.Clone();
            c.X.Should().Be(7);
            c.Y.Should().Be(8);
            c.Should().NotBeSameAs(p);
        }

        [Fact]
        public void TPoint_ToString_HasExpectedFormat()
        {
            new TPoint(1, 2).ToString().Should().Be("(1, 2)");
        }

        [Fact]
        public void Geom_GetCenter_ReturnsRectangleCenter()
        {
            var r = new Rectangle(0, 0, 100, 50);
            var center = r.GetCenter();
            center.X.Should().Be(50);
            center.Y.Should().Be(25);
        }

        [Fact]
        public void Geom_CircleToRectangle_ProducesBoundingBox()
        {
            var rect = Geom.CircleToRectangle(new TPoint(50, 50), radius: 10);
            rect.X.Should().Be(40);
            rect.Y.Should().Be(40);
            rect.Width.Should().Be(20);
            rect.Height.Should().Be(20);
        }

        [Fact]
        public void Geom_LengthOfSizeF_IsPythagorean()
        {
            new SizeF(3, 4).Length().Should().BeApproximately(5f, 0.0001f);
        }

        [Fact]
        public void Geom_Multiply_ScalesSize()
        {
            new SizeF(2, 3).Multiply(2f).Should().Be(new SizeF(4, 6));
        }

        [Fact]
        public void Geom_RadToDeg_RoundtripsThroughKnownValue()
        {
            Geom.RadToDeg((float)System.Math.PI).Should().BeApproximately(180f, 0.0001f);
        }

        [Fact]
        public void TRectangle_FromPointList_ComputesBounds()
        {
            var rect = TRectangle.FromPointList(new[]
            {
                new TPoint(1, 2),
                new TPoint(5, 1),
                new TPoint(3, 6),
            });

            rect.Left.Should().Be(1);
            rect.Top.Should().Be(1);
            rect.Right.Should().Be(5);
            rect.Bottom.Should().Be(6);
            rect.Size.Should().Be(new Size(4, 5));
        }
    }
}
