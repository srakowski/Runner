﻿//   
// Copyright (c) Jesse Freeman. All rights reserved.  
//  
// Licensed under the Microsoft Public License (MS-PL) License. 
// See LICENSE file in the project root for full license information. 
// 
// Contributors
// --------------------------------------------------------
// This is the official list of Pixel Vision 8 contributors:
//  
// Jesse Freeman - @JesseFreeman
// Christer Kaitila - @McFunkypants
// Pedro Medeiros - @saint11
// Shawn Rakowski - @shwany

using System;
using System.Collections.Generic;
using PixelVisionSDK;
using PixelVisionSDK.Chips;

namespace PixelVisionRunner.Parsers
{
    public class TilemapJsonParser : JsonParser
	{
		protected IEngine target;
		
		public TilemapJsonParser(string jsonString, IEngine target) : base(jsonString)
		{
			this.target = target;
		}
		
		public override void CalculateSteps()
		{
			base.CalculateSteps();
			steps.Add(ConfigureTilemap);
		}

		public virtual void ConfigureTilemap()
		{
			// TODO need to parse the json data

			var tilemapChip = target.tilemapChip;

			if (data.ContainsKey("layers"))
			{
//				Debug.Log("Parse tilemap json data");

				var layers = data["layers"] as List<object>;

				var total = layers.Count;

				for (int i = 0; i < total; i++)
				{

					var layer = layers[i] as Dictionary<string, object>;

					try
					{
						var layerType = (TilemapChip.Layer) Enum.Parse(typeof(TilemapChip.Layer), ((string)layer["name"]));

						var offset = layerType == TilemapChip.Layer.Sprites ? 1 : target.spriteChip.totalSprites + 1;
						
						var columns = (int) (long) layer["width"];
						var rows = (int) (long) layer["height"];

						var rawLayerData = layer["data"] as List<object>;
						
						int[] dataValues = rawLayerData.ConvertAll(x => ((int) (long)x) - offset < -1 ? -1 : ((int) (long)x) - offset).ToArray();
						
						if (tilemapChip.columns != columns || tilemapChip.rows != rows)
						{
							var tmpPixelData = new TextureData(columns, rows);
							tmpPixelData.SetPixels(0, 0, columns, rows, dataValues);
							
							Array.Resize(ref dataValues, tilemapChip.total);
							
							tmpPixelData.GetPixels(0, 0, tilemapChip.columns, tilemapChip.rows, ref dataValues);
						}
						
						Array.Copy(dataValues, tilemapChip.layers[(int)layerType], dataValues.Length); 
						
						// TODO need to make sure that the layer is the same size as the display chip

						// TODO copy the tilemap data over to layer correctly

					}
					catch (Exception e)
					{
						// Just igonre any layers that don't exist
						throw new Exception("Unable to parse 'tilemap.json' file. It may be corrupt. Try deleting it and creating a new one.");
					}
					

				}
				
//				if (data.ContainsKey("width"))
//				{
//				}
				
			}
			
			
			
			currentStep++;

		}
	}
}