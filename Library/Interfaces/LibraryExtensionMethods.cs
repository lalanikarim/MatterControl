﻿/*
Copyright (c) 2017, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Threading.Tasks;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.Library
{
	public static class LibraryExtensionMethods
	{
		public static Task<IObject3D> CreateContent(this ILibraryItem libraryItem, Action<double, string> progressReporter)
		{
			if (ApplicationController.Instance.Library.GetContentProvider(libraryItem) is ISceneContentProvider contentProvider)
			{
				return contentProvider?.CreateItem(libraryItem, progressReporter);
			}

			return Task.FromResult<IObject3D>(null);
		}

		public static bool IsContentFileType(this ILibraryItem item)
		{
			return item is ILibraryObject3D
				|| item is SDCardFileItem
				|| item is PrintHistoryItem
				|| (item is ILibraryAssetStream contentStream
					&& ApplicationController.Instance.Library.IsContentFileType(contentStream.FileName));
		}

		public static bool IsMeshFileType(this ILibraryItem item)
		{
			return item is ILibraryAssetStream contentStream
					&& ApplicationController.Instance.Library.IsMeshFileType(contentStream.FileName);
		}
	}
}
