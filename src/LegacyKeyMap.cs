using System.Collections.Generic;
namespace InputAtlas {
// Optional WhatPulse/Qt key-identifier compatibility mapping.
public static class LegacyKeyMap {
 static readonly Dictionary<int,int> Special=new Dictionary<int,int>{
  {0x01000000,27},{0x01000001,9},{0x01000003,8},{0x01000004,13},{0x01000005,269},
  {0x01000006,45},{0x01000007,46},{0x01000008,19},{0x01000009,44},
  {0x01000010,36},{0x01000011,35},{0x01000012,37},{0x01000013,38},{0x01000014,39},{0x01000015,40},{0x01000016,33},{0x01000017,34},
  {0x01000024,20},{0x01000025,144},{0x01000026,145},{0x01000053,91},{0x01000054,92},{0x01000055,93},
  {16781904,92},{16781905,91},{16781914,165},{16781915,164},{16781916,163},{16781917,162},{16781921,161},{16781922,160},
  {16781912,96},{16781920,97},{16781909,98},{16781906,99},{16781911,100},{16781908,101},{16781919,102},{16781918,103},{16781913,104},{168,105},{16781910,269},
  {16777429,106},{16777428,107},{202,109},{16777471,110},{16777474,111},{16777476,144},{195,192}
 };
 public static int Map(int key){
  if(key==32||(key>=48&&key<=57)||(key>=65&&key<=90))return key;
  if(key>=0x01000030&&key<=0x01000047)return 112+key-0x01000030;
  int value;if(Special.TryGetValue(key,out value))return value;
  switch(key){case 39:return 222;case 44:return 188;case 45:return 189;case 46:return 190;case 47:return 191;case 59:return 186;case 61:return 187;case 91:return 219;case 92:return 220;case 93:return 221;case 96:return 223;default:return 0;}
 }
 public static int Button(int key){return key>=0&&key<=2?key+1:10000+key;}
}
}
