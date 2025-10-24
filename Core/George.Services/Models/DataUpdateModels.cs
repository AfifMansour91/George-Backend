using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace George.Services
{


	public class AddressReq
	{
		[JsonProperty("whereValues")]
		public List<string> WhereValues { get; set; }

		[JsonProperty("locateType")]
		public int LocateType { get; set; }
	}

	public class AddressRes
	{
		public int ErrorCode { get; set; }
		public int Status { get; set; }
		public string Message { get; set; }
		public Data Data { get; set; }
		public bool Active { get; set; }
	}

	public class Data
	{
		public int? ShapeIndex { get; set; }
		public List<ValueItem> Values { get; set; }
		public object ColumnsToSkip { get; set; }
		public List<string> FieldsMapping { get; set; }
		public int CentroidFieldIndex { get; set; }
		public Extent Extent { get; set; }
		public ParamRequest ParamRequest { get; set; }
		public string ErrMsg { get; set; }
		public string Exception { get; set; }
		public int ErrorCode { get; set; }
	}

	public class ValueItem
	{
		public int ObjectId { get; set; }
		public DateTime Created { get; set; }
		public bool IsEditable { get; set; }
		public List<object> Values { get; set; }
	}

	public class Extent
	{
		public double Xmin { get; set; }
		public double Ymin { get; set; }
		public double Xmax { get; set; }
		public double Ymax { get; set; }
	}

	public class ParamRequest
	{
		public string ConnectionName { get; set; }
		public string TableName { get; set; }
		public List<string> Fields { get; set; }
		public string WhereClause { get; set; }
		public int MaxResult { get; set; }
		public object Wkt { get; set; }
		public string Srid { get; set; }
		public int IntersectionType { get; set; }
		public int QueryType { get; set; }
		public string GeometryName { get; set; }
		public string TargetTableName { get; set; }
		public string GeometryTargetName { get; set; }
		public bool ReturnWkt { get; set; }
		public bool ReturnExtent { get; set; }
		public double MapTolerance { get; set; }
		public bool Centroid { get; set; }
		public bool Distinct { get; set; }
		public int GeometryType { get; set; }
		public int TargetGeometryType { get; set; }
		public int Skip { get; set; }
		public int Buffer { get; set; }
		public object OrderBy { get; set; }
		public object Permission { get; set; }
		public object TargetWhereClause { get; set; }
		public bool IsReturnOnlyCount { get; set; }
	}


}
