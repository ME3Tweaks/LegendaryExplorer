using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Animation;

namespace LegendaryExplorer.SharedUI.Bases
{
    public static class WPFExtensions
    {
        /// <summary>
        /// Binds a property
        /// </summary>
        /// <param name="bound">object to create binding on</param>
        /// <param name="boundProp">property to create binding on</param>
        /// <param name="source">object being bound to</param>
        /// <param name="sourceProp">property being bound to</param>
        /// <param name="converter">optional value converter</param>
        /// <param name="parameter">optional value converter parameter</param>
        public static void bind(this FrameworkElement bound, DependencyProperty boundProp, FrameworkElement source, string sourceProp,
            IValueConverter converter = null, object parameter = null)
        {
            Binding b = new Binding { Source = source, Path = new PropertyPath(sourceProp) };
            if (converter != null)
            {
                b.Converter = converter;
                b.ConverterParameter = parameter;
            }
            bound.SetBinding(boundProp, b);
        }

        /// <summary>
        /// Starts a DoubleAnimation for a specified animated property on this element
        /// </summary>
        /// <param name="target">element to perform animation on</param>
        /// <param name="dp">The property to animate, which is specified as a dependency property identifier</param>
        /// <param name="toValue">The destination value of the animation</param>
        /// <param name="duration">The duration of the animation, in milliseconds</param>
        public static void BeginDoubleAnimation(this UIElement target, DependencyProperty dp, double toValue, int duration)
        {
            target.BeginAnimation(dp, new DoubleAnimation(toValue, TimeSpan.FromMilliseconds(duration)));
        }

        /// <summary>
        /// Overwrites a portion of an array starting at offset with the contents of another array.
        /// </summary>
        /// <typeparam name="T">Content of array.</typeparam>
        /// <param name="dest">Array to write to</param>
        /// <param name="offset">Start index in dest</param>
        /// <param name="source">data to write to dest</param>
        public static void OverwriteRange<T>(this T[] dest, int offset, T[] source)
        {
            if (offset + source.Length > dest.Length)
            {
                throw new IndexOutOfRangeException("Attempt to write past the end of the array.");
            }
            for (int i = 0; i < source.Length; i++)
            {
                dest[offset + i] = source[i];
            }
        }

        //based on algorithm described here: http://www.codeproject.com/Articles/13525/Fast-memory-efficient-Levenshtein-algorithm
        public static int LevenshteinDistance(this string a, string b)
        {
            int n = a.Length;
            int m = b.Length;
            if (n == 0)
            {
                return m;
            }
            else if (m == 0)
            {
                return n;
            }
            int[] v0;
            int[] v1 = new int[m + 1];
            for (int i = 0; i <= m; i++)
            {
                v1[i] = i;
            }
            int above;
            int left;
            int cost;
            for (int i = 1; i <= n; i++)
            {
                v0 = v1;
                v1 = new int[m + 1];
                v1[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    above = v1[j - 1] + 1;
                    left = v0[j] + 1;
                    if (j > m || j > n)
                    {
                        cost = 1;
                    }
                    else
                    {
                        cost = a[j - 1] == b[j - 1] ? 0 : 1;
                    }
                    cost += v0[j - 1];
                    v1[j] = Math.Min(above, Math.Min(left, cost));

                }
            }
            
            return v1[m];
        }

        public static bool FuzzyMatch(this IEnumerable<string> words, string word, double threshold = 0.75)
        {
            int dist;
            foreach (string s in words)
            {
                dist = s.LevenshteinDistance(word);
                if (1 - (double)dist / Math.Max(s.Length, word.Length) > threshold)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
