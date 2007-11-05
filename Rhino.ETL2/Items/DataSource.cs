using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using Boo.Lang;
using Boo.Lang.Compiler.MetaProgramming;
using Rhino.ETL.Engine;

namespace Rhino.ETL
{
	using Retlang;

	public class DataSource : BaseDataElement<DataSource>
	{
		private const string DefaultOutputName = "Output";
		protected ICallable blockToExecute;

		private string outputName = DefaultOutputName;

		public string OutputName
		{
			get { return outputName; }
			set { outputName = value; }
		}

		public DataSource(string name)
			: base(name)
		{
			EtlConfigurationContext.Current.AddSource(name, this);
		}

		public override void Start(IProcessContext context, string input)
		{
			Items[ProcessContextKey] = context;
			if (blockToExecute != null)
			{
				using (EnterContext())
				{
					blockToExecute.Call(new object[] { this });
				}
			}
			else
			{
				ReadFromDatabase(context);
			}
			context.Publish(OutputName + Messages.Done, Messages.Done);
			context.Stop();
		}

		/// <summary>
		/// Custom behaviors can be had by overriding it using a block
		/// in the source element in the DSL
		/// </summary>
		protected virtual void ReadData(IProcessContext context)
		{
			ReadFromDatabase(context);
		}

		private void ReadFromDatabase(IProcessContext context)
		{
			using (IDbConnection connection = ConnectionFactory.AcquireConnection())
			using (IDbCommand command = connection.CreateCommand())
			{
				command.CommandText = Command;
				AddParameters(command);

				using (IDataReader reader = command.ExecuteReader())
				{
					DataTable schema = reader.GetSchemaTable();
					List<string> columns = new List<string>();
					foreach (DataRow schemaRow in schema.Rows)
					{
						columns.Add((string)schemaRow["ColumnName"]);
					}
					while (reader.Read())
					{
						Row row = new Row();
						for (int i = 0; i < columns.Count; i++)
						{
							object value = reader.GetValue(i);
							if (value == DBNull.Value)
								value = null;
							row[columns[i]] = value;
						}
						SendRow(row);
					}
				}
			}
		}
		public void SendRow(Row row)
		{
			SendRow(ProcessContextFromCurrentContext, OutputName, row);
		}

		public void SendRow(string name, Row row)
		{
			SendRow(ProcessContextFromCurrentContext, name, row);
		}

		private static void SendRow(IObjectPublisher context, string name, Row row)
		{
			context.Publish(name, row);
		}

		public void Execute(ICallable block)
		{
			blockToExecute = block;
		}

		public void execute(ICallable block)
		{
			Execute(block);
		}


		protected override bool CustomActionSpecified
		{
			get { return blockToExecute != null; }
		}
	}
}