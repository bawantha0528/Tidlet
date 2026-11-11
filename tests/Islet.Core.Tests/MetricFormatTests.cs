using System.Globalization;
using Islet.Core.Bar;
namespace Islet.Core.Tests;

[TestClass]
public sealed class MetricFormatTests
{
    [TestMethod]
    public void TransferUnitsUseBytesOrBitsWithoutChangingSamplesAndRejectInvalidValues()
    {
        var previous=CultureInfo.CurrentCulture;try
        {
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
            Assert.AreEqual("0 B/s",MetricFormat.TransferRate(0));Assert.AreEqual("1.0 KiB/s",MetricFormat.TransferRate(1024));
            Assert.AreEqual("1.0 MiB/s",MetricFormat.TransferRate(1048576));Assert.AreEqual("1.0 Mb/s",MetricFormat.TransferRate(125000,true));
            Assert.AreEqual("1.0 Gb/s",MetricFormat.TransferRate(125000000,true));Assert.AreEqual("0 b/s",MetricFormat.TransferRate(0,true));
            foreach(var invalid in new double?[]{null,double.NaN,double.PositiveInfinity,double.NegativeInfinity,-1,double.MaxValue})
            {Assert.AreEqual("Missing",MetricFormat.TransferRate(invalid,false,"Missing"));Assert.AreEqual("Missing",MetricFormat.TransferRate(invalid,true,"Missing"));}
        }finally{CultureInfo.CurrentCulture=previous;}
    }
}
