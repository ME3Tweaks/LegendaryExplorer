using System;
using Gammtek.Conduit.Extensions;

namespace Gammtek.Conduit.MassEffect.Coalesce
{
	public class CoalesceDocument
	{
		public CoalesceDocument(string name = null, CoalesceSections sections = null)
		{
			Name = name ?? nameof(CoalesceDocument);
			Sections = sections ?? new CoalesceSections();
		}

		public bool HasId => !Id.IsNullOrEmpty();

		public bool HasName => !Name.IsNullOrEmpty();

		public bool HasSource => !Source.IsNullOrEmpty();

		public string Id { get; set; }

		public string Name { get; set; }

		public CoalesceSections Sections { get; set; }

		public string Source { get; set; }

		public void Combine(CoalesceDocument document)
		{
			if (document == null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			foreach (var section in document.Sections)
			{
				if (!Sections.ContainsKey(section.Key))
				{
					Sections.Add(section.Key, section.Value);
				}
				else
				{
					Sections[section.Key].Combine(section.Value);
				}
			}
		}

		public bool CompareId(CoalesceDocument document, bool ignoreCase = false)
		{
			if (document == null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			return CompareId(document.Id, ignoreCase);
		}

		public bool CompareId(string id, bool ignoreCase = false)
		{
			if (!HasId || id.IsNullOrEmpty())
			{
				return false;
			}

			if (ignoreCase && Id.Equals(id, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return !ignoreCase && Id.Equals(id);
		}

		public bool CompareName(CoalesceDocument document, bool ignoreCase = false)
		{
			if (document == null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			return CompareName(document.Name, ignoreCase);
		}

		public bool CompareName(string name, bool ignoreCase = false)
		{
			if (!HasName || name.IsNullOrEmpty())
			{
				return false;
			}

			if (ignoreCase && Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return !ignoreCase && Name.Equals(name);
		}

		public bool CompareSource(CoalesceDocument document, bool ignoreCase = false)
		{
			if (document == null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			return CompareSource(document.Source, ignoreCase);
		}

		public bool CompareSource(string source, bool ignoreCase = false)
		{
			if (!HasSource || source.IsNullOrEmpty())
			{
				return false;
			}

			if (ignoreCase && Source.Equals(source, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return !ignoreCase && Source.Equals(source);
		}

		public void MergeRight(CoalesceDocument document)
		{
			if (document == null)
			{
				throw new ArgumentNullException(nameof(document));
			}

			throw new NotImplementedException();
		}
	}
}
