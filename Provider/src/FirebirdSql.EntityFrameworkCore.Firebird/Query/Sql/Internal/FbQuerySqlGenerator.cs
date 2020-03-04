﻿/*
 *    The contents of this file are subject to the Initial
 *    Developer's Public License Version 1.0 (the "License");
 *    you may not use this file except in compliance with the
 *    License. You may obtain a copy of the License at
 *    https://github.com/FirebirdSQL/NETProvider/blob/master/license.txt.
 *
 *    Software distributed under the License is distributed on
 *    an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either
 *    express or implied. See the License for the specific
 *    language governing rights and limitations under the License.
 *
 *    All Rights Reserved.
 */

//$Authors = Jiri Cincura (jiri@cincura.net), Jean Ressouche, Rafael Almeida (ralms@ralms.net)

using System;
using System.Linq;
using System.Linq.Expressions;
using FirebirdSql.EntityFrameworkCore.Firebird.Query.Expressions.Internal;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.Sql;
using Microsoft.EntityFrameworkCore.Storage;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Query.Sql.Internal
{
	public class FbQuerySqlGenerator : DefaultQuerySqlGenerator, IFbExpressionVisitor
	{
		public FbQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies, SelectExpression selectExpression)
			: base(dependencies, selectExpression)
		{ }

		protected override string TypedTrueLiteral => "TRUE";

		protected override string TypedFalseLiteral => "FALSE";

		protected override void GenerateTop(SelectExpression selectExpression)
		{
			if (selectExpression.Limit != null)
			{
				Sql.Append("FIRST ");
				Visit(selectExpression.Limit);
				Sql.Append(" ");
			}

			if (selectExpression.Offset != null)
			{
				Sql.Append("SKIP ");
				Visit(selectExpression.Offset);
				Sql.Append(" ");
			}
		}

		protected override void GenerateIn(InExpression inExpression, bool negated = false)
		{
			if (inExpression.Values.Any() && inExpression.Values.First() is ConstantExpression ce && ce.Type == typeof(Guid))
			{
				Visit(inExpression.Operand);
				Sql.Append(negated ? " NOT IN " : " IN ");
				Sql.Append("(");

				for (var index = 0; index < inExpression.Values.Count; index++)
				{
					if (index > 0)
						Sql.Append(",");

					var constantExpression = (ConstantExpression)inExpression.Values[index];

					Sql.Append("char_to_uuid('");

					var guid = (Guid) constantExpression.Value;

					Sql.Append(guid.ToString("D"));
					Sql.Append("')");
				}

				Sql.Append(")");
			}
			else
			{
				base.GenerateIn(inExpression, negated);
			}
		}

		protected override void GenerateLimitOffset(SelectExpression selectExpression)
		{
			// handled by GenerateTop
		}

		protected override Expression VisitBinary(BinaryExpression binaryExpression)
		{
			if (binaryExpression.NodeType == ExpressionType.Add && binaryExpression.Left.Type == typeof(string) && binaryExpression.Right.Type == typeof(string))
			{
				Sql.Append("(");
				Visit(binaryExpression.Left);
				Sql.Append(" || ");
				var exp = Visit(binaryExpression.Right);
				Sql.Append(")");
				return exp;
			}
			else
			{
				return base.VisitBinary(binaryExpression);
			}
		}

		public virtual Expression VisitSubstring(FbSubstringExpression substringExpression)
		{
			Sql.Append("SUBSTRING(");
			Visit(substringExpression.ValueExpression);
			Sql.Append(" FROM ");
			Visit(substringExpression.FromExpression);
			if (substringExpression.ForExpression != null)
			{
				Sql.Append(" FOR ");
				Visit(substringExpression.ForExpression);
			}
			Sql.Append(")");
			return substringExpression;
		}

		public virtual Expression VisitExtract(FbExtractExpression extractExpression)
		{
			Sql.Append("EXTRACT(");
			Sql.Append(extractExpression.Part);
			Sql.Append(" FROM ");
			Visit(extractExpression.ValueExpression);
			Sql.Append(")");
			return extractExpression;
		}

		public override Expression VisitSelect(SelectExpression selectExpression)
		{
			base.VisitSelect(selectExpression);
			if (!selectExpression.Tables.Any())
			{
				Sql.AppendLine();
				Sql.Append("FROM RDB$DATABASE");
			}
			return selectExpression;
		}
	}
}
