﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace BluwolfIcons
{
	/// <summary>
	/// Represents an icon image file.
	/// </summary>
	public sealed class Icon
	{
		public IList<IIconImage> Images { get; } = new List<IIconImage>();

		/// <summary>
		/// Saves this icon to a specified file.
		/// </summary>
		/// <param name="fileName">The file to save this icon to.</param>
		public void Save(string fileName)
		{
			using (var stream = File.Open(fileName, FileMode.Create))
			{
				Save(stream);
			}
		}

		/// <summary>
		/// Saves this icon to a specified stream.
		/// </summary>
		/// <param name="stream">The stream to save this icon to.</param>
		public void Save(Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));

			if (!stream.CanSeek)
				throw new ArgumentException("Stream must support seeking.", nameof(stream));

			using (var writer = new BinaryWriter(stream))
			{
				// Reserved, always 0.
				writer.Write((ushort)0);
				// 1 for ICO, 2 for CUR
				writer.Write((ushort)1);
				writer.Write((ushort)Images.Count);

				// We'll store images here with a stream position indicating where to write specific image data later
				var pendingImages = new Dictionary<long, IIconImage>();

				foreach (var image in Images)
				{
					writer.Write((byte)image.Width);
					writer.Write((byte)image.Height);

					// Number of colors in the palette. Since we always save the image ourselves (with no palette), hardcode this to 0 (No palette).
					writer.Write((byte)0);
					// Reserved, always 0.
					writer.Write((byte)0);
					// Color planes. Since we save the images ourselves, this is 1.
					writer.Write((ushort)1);
					writer.Write((ushort)image.BitsPerPixel);

					pendingImages.Add(writer.BaseStream.Position, image);

					// Image size in bytes. Since we can't know this yet, fill it with zeros.
					writer.Write(0u);
					// Image data offset from the start of the file. Since we can't know this yet, fill it with zeros.
					writer.Write(0u);
				}

				foreach (var image in pendingImages)
				{
					var data = image.Value.GetData();
					var offset = (uint)writer.BaseStream.Position;

					// We need to write specific image data now.
					writer.BaseStream.Seek(image.Key, SeekOrigin.Begin);
					writer.Write((uint)data.Length);
					writer.Write(offset);

					// Return to the proper stream location and write the image data.
					writer.BaseStream.Seek(offset, SeekOrigin.Begin);
					writer.Write(data);
				}
			}
		}

		/// <summary>
		/// Loads an icon from a specified file.
		/// </summary>
		/// <param name="fileName">The file to load the icon from.</param>
		/// <returns>The loaded icon.</returns>
		public static Icon Load(string fileName)
		{
			using (var stream = File.OpenRead(fileName))
				return Load(stream);
		}

		/// <summary>
		/// Loads an icon from a stream.
		/// </summary>
		/// <param name="stream">The stream to load the icon from.</param>
		/// <returns>The loaded icon.</returns>
		public static Icon Load(Stream stream)
			=> Load(new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad));

		/// <summary>
		/// Loads an icon from a bitmap decoder.
		/// </summary>
		/// <param name="decoder">The decoder to use when loading the icon. Every Frame decoded will be recognized as one individual image.</param>
		/// <returns>The loaded icon</returns>
		public static Icon Load(BitmapDecoder decoder)
		{
			if (decoder == null)
				throw new ArgumentNullException(nameof(decoder));

			var result = new Icon();

			foreach (var frame in decoder.Frames)
			{
				result.Images.Add(new PngIconImage(frame));
				result.Images.Add(new BmpIconImage(frame));
			}

			return result;
		}
	}
}
