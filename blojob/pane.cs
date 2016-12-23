﻿
using arookas.IO.Binary;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;

namespace arookas {

	public class bloPane : IEnumerable<bloPane> {

		protected bloPane mParent;
		protected List<bloPane> mChildren;
		protected uint mName;
		protected bool mVisible;
		protected bloRectangle mRect;
		protected gxCullMode mCullMode;
		protected bloAnchor mAnchor;
		protected double mAngle;
		protected byte mAlpha;
		protected bool mInheritAlpha;
		protected bool mConnectParent;

		protected byte mCumulativeAlpha;

		public bloPane() {
			mChildren = new List<bloPane>(10);
		}

		public void load(bloPane parentPane, object source, bloFormat format) {
			mParent = parentPane;
			if (mParent != null) {
				mParent.mChildren.Add(this);
			}
			mCullMode = gxCullMode.None;
			mConnectParent = false;
			switch (format) {
				case bloFormat.Compact: loadCompact(source as aBinaryReader); break;
				case bloFormat.Blo1: loadBlo1(source as aBinaryReader); break;
				default: throw new NotImplementedException("Format is not implemented.");
			}
		}

		protected virtual void loadCompact(aBinaryReader reader) {
			if (reader == null) {
				throw new ArgumentNullException("reader");
			}
			mVisible = (reader.Read8() != 0);
			reader.Step(1);

			mName = reader.Read32();

			int left = reader.ReadS16();
			int top = reader.ReadS16();
			int width = reader.ReadS16();
			int height = reader.ReadS16();
			mRect.set(left, top, (left + width), (top + height));

			setAnchor(bloAnchor.TopLeft);
			mAngle = 0.0d;
			mAlpha = 255;
			mInheritAlpha = true;
		}
		protected virtual void loadBlo1(aBinaryReader reader) {
			if (reader == null) {
				throw new ArgumentNullException("reader");
			}

			int numparams = reader.Read8();
			mVisible = (reader.Read8() != 0);
			reader.Step(2);

			mName = reader.Read32();

			int left = reader.ReadS16();
			int top = reader.ReadS16();
			int width = reader.ReadS16();
			int height = reader.ReadS16();
			mRect.set(left, top, (left + width), (top + height));

			numparams -= 6;

			if (numparams > 0) {
				mAngle = reader.Read16();
				--numparams;
			} else {
				mAngle = 0.0d;
			}

			if (numparams > 0) {
				mAnchor = (bloAnchor)reader.Read8();
				--numparams;
			} else {
				mAnchor = bloAnchor.TopLeft;
			}

			if (numparams > 0) {
				mAlpha = reader.Read8();
				--numparams;
			} else {
				mAlpha = 255;
			}

			if (numparams > 0) {
				mInheritAlpha = (reader.Read8() != 0);
				--numparams;
			} else {
				mInheritAlpha = true;
			}

			reader.Skip(4);
		}

		public virtual void saveBlo1(aBinaryWriter writer) {
			if (writer == null) {
				throw new ArgumentNullException("writer");
			}

			byte numparams;

			if (!mInheritAlpha) {
				numparams = 10;
			} else if (mAlpha < 255) {
				numparams = 9;
			} else if (mAnchor != bloAnchor.TopLeft) {
				numparams = 8;
			} else if (mAngle != 0.0d) {
				numparams = 7;
			} else {
				numparams = 6;
			}

			writer.Write8(numparams);
			writer.Write8((byte)(mVisible ? 1 : 0));
			writer.Step(2);
			writer.Write32(mName);
			writer.WriteS16((short)mRect.left);
			writer.WriteS16((short)mRect.top);
			writer.WriteS16((short)mRect.width);
			writer.WriteS16((short)mRect.height);

			numparams -= 6;

			if (numparams > 0) {
				writer.Write16((ushort)mAngle);
				--numparams;
			}

			if (numparams > 0) {
				writer.Write8((byte)mAnchor);
				--numparams;
			}

			if (numparams > 0) {
				writer.Write8(mAlpha);
				--numparams;
			}

			if (numparams > 0) {
				writer.Write8((byte)(mInheritAlpha ? 1 : 0));
				--numparams;
			}

			writer.WritePadding(4, 0);
		}
		public virtual void saveXml(XmlWriter writer) {
			if (writer == null) {
				throw new ArgumentNullException("writer");
			}

			if (mName != 0u) {
				writer.WriteAttributeString("id", convertNameToString(mName));
			}
			
			if (mConnectParent) {
				writer.WriteAttributeString("connect", mConnectParent.ToString());
			}

			if (!mVisible) {
				writer.WriteAttributeString("visible", mVisible.ToString());
			}

			bloXml.saveRectangle(writer, mRect, "rectangle");

			if (mAngle != 0.0d) {
				writer.WriteElementString("angle", ((ushort)mAngle).ToString());
			}

			if (mAnchor != bloAnchor.TopLeft) {
				writer.WriteElementString("anchor", mAnchor.ToString());
			}

			if (mAlpha != 255 || !mInheritAlpha) {
				writer.WriteStartElement("alpha");
				writer.WriteAttributeString("inherit", mInheritAlpha.ToString());
				writer.WriteValue(mAlpha);
				writer.WriteEndElement();
			}
		}
		
		public void move(bloPoint point) {
			move(point.x, point.y);
		}
		public virtual void move(int x, int y) {
			mRect.move(x, y);
		}
		public void add(bloPoint point) {
			add(point.x, point.y);
		}
		public virtual void add(int x, int y) {
			mRect.add(x, y);
		}
		public virtual void resize(int width, int height) {
			mRect.resize(width, height);
		}
		public virtual void reform(int left, int top, int right, int bottom) {
			mRect.reform(left, top, right, bottom);
		}
		public virtual bloPane search(uint name) {
			if (mName == name) {
				return this;
			}
			foreach (var child in mChildren) {
				var found = child.search(name);
				if (found != null) {
					return found;
				}
			}
			return null;
		}

		Vector2d getAnchorOffset() {
			var anchor = new Vector2d();
			switch ((int)mAnchor % 3) {
				case 0: anchor.X = 0; break;
				case 1: anchor.X = ((mRect.right - mRect.left) / 2); break;
				case 2: anchor.X = (mRect.right - mRect.left); break;
			}
			switch ((int)mAnchor / 3) {
				case 0: anchor.Y = 0; break;
				case 1: anchor.Y = ((mRect.bottom - mRect.top) / 2); break;
				case 2: anchor.Y = (mRect.bottom - mRect.top); break;
			}
			return anchor;
		}

		void setMatrix() {
			GL.Translate(mRect.left, mRect.top, 0.0d);
			if (mAngle != 0.0d) {
				var anchor = getAnchorOffset();
				GL.Translate(anchor.X, anchor.Y, 0.0d);
				GL.Rotate(-mAngle, Vector3d.UnitZ);
				GL.Translate(-anchor.X, -anchor.Y, 0.0d);
			}
		}
		void setAlpha() {
			mCumulativeAlpha = mAlpha;
			if (mParent != null && mInheritAlpha) {
				mCumulativeAlpha = (byte)((mAlpha * mParent.mCumulativeAlpha) / 256);
			}
		}

		public void loadGL() {
			loadGLSelf();
			foreach (var child in mChildren) {
				child.loadGL();
			}
		}
		protected virtual void loadGLSelf() {
			// empty
		}

		public void draw() {
			var context = bloContext.getContext();

			if ((!mVisible && !context.hasRenderFlags(bloRenderFlags.ShowInvisible)) || mRect.isEmpty()) {
				return;
			}

			GL.PushMatrix();
			setMatrix();
			setAlpha();
			gl.setCullMode(gxCullMode.None);
			drawSelf();

			foreach (var child in mChildren) {
				child.draw();
			}

			GL.PopMatrix();
		}
		protected virtual void drawSelf() {
			var context = bloContext.getContext();

			if (!context.hasRenderFlags(bloRenderFlags.PaneWireframe)) {
				return;
			}

			GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

			GL.Begin(PrimitiveType.Quads);
			GL.Color4(Color4.White);
			GL.Vertex3(mRect.left, mRect.top, 0.0d);
			GL.Color4(Color4.White);
			GL.Vertex3(mRect.right, mRect.top, 0.0d);
			GL.Color4(Color4.White);
			GL.Vertex3(mRect.right, mRect.bottom, 0.0d);
			GL.Color4(Color4.White);
			GL.Vertex3(mRect.left, mRect.bottom, 0.0d);
			GL.End();

			GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
		}

		public bloPane getParentPane() {
			return mParent;
		}
		public int getChildPane() {
			return mChildren.Count;
		}
		public bloPane getChildPane(int index) {
			return mChildren[index];
		}
		public TPane getChildPane<TPane>(int index) where TPane : bloPane {
			return (mChildren[index] as TPane);
		}

		public uint getName() {
			return mName;
		}
		public bool getVisible() {
			return mVisible;
		}
		public bloRectangle getRectangle() {
			return mRect;
		}
		public gxCullMode getCullMode() {
			return mCullMode;
		}
		public bloAnchor getAnchor() {
			return mAnchor;
		}
		public double getAngle() {
			return mAngle;
		}
		public byte getAlpha() {
			return mAlpha;
		}
		public bool getInheritAlpha() {
			return mInheritAlpha;
		}
		public bool getConnectParent() {
			return mConnectParent;
		}

		public uint setName(uint name) {
			uint old = mName;
			mName = name;
			return old;
		}
		public bool setVisible(bool visible) {
			bool old = mVisible;
			mVisible = visible;
			return old;
		}
		public bloRectangle setRectangle(bloRectangle rectangle) {
			bloRectangle old = mRect;
			mRect = rectangle;
			return old;
		}
		public void setCullMode(gxCullMode cull) {
			mCullMode = cull;
		}
		public void setAnchor(bloAnchor anchor) {
			mAnchor = anchor;
		}
		public double setAngle(double angle) {
			double old = mAngle;
			mAngle = angle;
			return old;
		}
		public byte setAlpha(byte alpha) {
			byte old = mAlpha;
			mAlpha = alpha;
			return old;
		}
		public bool setInheritAlpha(bool set) {
			bool old = mInheritAlpha;
			mInheritAlpha = set;
			return old;
		}
		public virtual bool setConnectParent(bool set) {
			mConnectParent = false;
			return false;
		}

		public virtual void info() {
			Console.WriteLine("Name : 0x{0:X8} '{1}{2}{3}{4}'", mName, (char)((mName >> 24) & 255), (char)((mName >> 16) & 255), (char)((mName >> 8) & 255), (char)((mName >> 0) & 255));
			Console.Write("Rectangle :");
			Console.Write(" {0}, {1}, {2}, {3}", mRect.left, mRect.top, mRect.right, mRect.bottom);
			Console.Write(" ({0}, {1}) : ({2}x{3})", mRect.left, mRect.top, mRect.width, mRect.height);
			Console.WriteLine();
			if (mAngle != 0.0d) {
				Console.WriteLine("Angle : {1:N2}°", mAngle);
			}
			Console.WriteLine("Anchor : {0}", mAnchor);
			Console.WriteLine("Cull Mode : {0}", mCullMode);
			Console.WriteLine("Alpha : {0:P1}", (mAlpha / 255.0d));
			Console.WriteLine("Visible : {0}", mVisible);
			Console.Write("Flags :");
			if (mInheritAlpha) {
				Console.Write(" (inherit alpha)");
			}
			if (mConnectParent) {
				Console.Write(" (connect parent)");
			}
			Console.WriteLine();
		}

		public IEnumerator<bloPane> GetEnumerator() {
			return mChildren.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		internal static uint convertStringToName(string str) {
			var name = 0u;
			for (int i = 0; i < str.Length; ++i) {
				name <<= 8;
				name |= (uint)(str[i] & 255);
			}
			return name;
		}
		internal static string convertNameToString(uint name) {
			if (name > 0xFFFFFFu) {
				var chars = new char[4];
				chars[0] = (char)((name >> 24) & 255);
				chars[1] = (char)((name >> 16) & 255);
				chars[2] = (char)((name >> 8) & 255);
				chars[3] = (char)((name >> 0) & 255);
				return new String(chars);
			} else if (name > 0xFFFFu) {
				var chars = new char[3];
				chars[0] = (char)((name >> 16) & 255);
				chars[1] = (char)((name >> 8) & 255);
				chars[2] = (char)((name >> 0) & 255);
				return new String(chars);
			} else if (name > 0xFFu) {
				var chars = new char[2];
				chars[0] = (char)((name >> 8) & 255);
				chars[1] = (char)((name >> 0) & 255);
				return new String(chars);
			} else if (name > 0u) {
				var chars = new char[1];
				chars[0] = (char)((name >> 0) & 255);
				return new String(chars);
			}
			return "";
		}

	}

}
