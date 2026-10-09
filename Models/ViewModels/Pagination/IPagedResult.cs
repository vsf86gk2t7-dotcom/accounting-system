namespace AccountingSystem.Models.ViewModels.Pagination
{
	public interface IPagedResult
	{
		int PageNumber { get; }
		int PageSize { get; }
		int TotalCount { get; }
		int TotalPages { get; }
		bool HasPreviousPage { get; }
		bool HasNextPage { get; }
	}
}