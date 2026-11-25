using HrSystemDhg.Web.Services;
using Xunit;
public class ListPagerTests
{
    [Fact]
    public void EmptyAndExactPagesHaveCorrectBoundaries()
    {
        var empty=new ListPager(null,99,0);Assert.Equal(1,empty.PageNumber);Assert.Equal(0,empty.First);Assert.Equal(0,empty.Last);
        var exact=new ListPager("20",2,40);Assert.Equal(2,exact.TotalPages);Assert.Equal(21,exact.First);Assert.Equal(40,exact.Last);
    }
    [Theory]
    [InlineData("20",20)][InlineData("50",50)][InlineData("100",100)][InlineData("200",200)]
    public void LastPageDoesNotExceedMatchingCount(string size,int count)
    {
        var pager=new ListPager(size,int.MaxValue,count+1);
        Assert.Equal(2,pager.PageNumber);Assert.Equal(count+1,pager.First);Assert.Equal(count+1,pager.Last);
    }
    [Fact]
    public void AllShowsEveryMatchingRecordAndInvalidValuesFallBackSafely()
    {
        var all=new ListPager("all",999,601);Assert.Null(all.Size);Assert.Equal(1,all.PageNumber);Assert.Equal(601,all.Last);
        var invalid=new ListPager("1000000",-8,601);Assert.Equal(20,invalid.Size);Assert.Equal(1,invalid.PageNumber);
    }
}
