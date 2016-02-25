using Caliburn.Micro;

namespace Gammtek.Conduit.MassEffect.DifficultyEditor.Models
{
	public class Vector2D : PropertyChangedBase
	{
		private float _x;
		private float _y;

		public Vector2D(float x = 0f, float y = 0f)
		{
			_x = x;
			_y = y;
		}

		public float X
		{
			get { return _x; }
			set
			{
				if (value.Equals(_x))
				{
					return;
				}
				_x = value;
				NotifyOfPropertyChange(() => X);
			}
		}

		public float Y
		{
			get { return _y; }
			set
			{
				if (value.Equals(_y))
				{
					return;
				}
				_y = value;
				NotifyOfPropertyChange(() => Y);
			}
		}
	}
}
