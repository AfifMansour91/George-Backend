using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using George.Common;
using George.DB;

namespace George.Data
{
	public class PagingDto
	{
		public PagingDto()
		{
			Skip = 0;
			Take = 10;
		}
		public PagingDto(int take)
		{
			Skip = 0;
			Take = take;
		}

		[DefaultValue(0)]
		[Range(0, int.MaxValue)]
		public int Skip { get; set; }

		[DefaultValue(10)]
		public int Take { get; set; } = 10;
	}

	public class PagingExDto : PagingDto
	{
		public PagingExDto()
		{
			Skip = 0;
			Take = 10;
			IncludeTotal = false;
			SortOrder = SortOrder.Ascending;
		}
		public PagingExDto(int take)
		{
			Skip = 0;
			Take = take;
			IncludeTotal = false;
			SortOrder = SortOrder.Ascending;
		}

		public SortField SortField { get; set; } = SortField.None;

		[DefaultValue(SortOrder.Ascending)]
		public SortOrder SortOrder { get; set; } = SortOrder.Ascending;

		[DefaultValue(false)]
		public bool IncludeTotal { get; set; } = false;
	}

	public class DataListResult<TEntity>
	{
		public List<TEntity> Items { get; set; } = new List<TEntity>();
		public int? Total { get; set; }
	}

	public class StorageResult<T>
	{
		public bool IsSuccessful => (this.StatusCode == (int)StatusCode.Ok);
		public StatusCode StatusCode { get; set; } = StatusCode.Ok;
		public T? Data { get; set; }
	}

	public class IdNamePair
	{
		public IdNamePair() 
		{ 
			Id = 0; 
			Name = string.Empty; 
		}
		public IdNamePair(int id, string name) 
		{ 
			Id = id; 
			Name = name; 
		}

		public int Id { get; set; }
		public string Name { get; set; }
	}


	
}
