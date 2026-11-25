namespace HrSystemDhg.Web.Services;
public sealed class ListPager
{
    public static readonly string[] Options=["20","50","100","200","All"];
    public string RowsPerPage { get; }
    public int? Size { get; }
    public int TotalRecords { get; }
    public int TotalPages { get; }
    public int PageNumber { get; }
    public int Skip => Size.HasValue?(PageNumber-1)*Size.Value:0;
    public int First => TotalRecords==0?0:Skip+1;
    public int Last => (int)Math.Min(TotalRecords,(long)Skip+(Size??TotalRecords));
    public ListPager(string? rows,int page,int total)
    {
        RowsPerPage=Options.FirstOrDefault(value=>string.Equals(value,rows,StringComparison.OrdinalIgnoreCase))??"20";
        Size=RowsPerPage=="All"?null:int.Parse(RowsPerPage);
        TotalRecords=Math.Max(0,total);
        TotalPages=Size.HasValue && TotalRecords>0?(TotalRecords-1)/Size.Value+1:1;
        PageNumber=Math.Clamp(page,1,TotalPages);
    }
}
public sealed record ListPagerView(ListPager Paging,Func<int,string> PageUrl);
