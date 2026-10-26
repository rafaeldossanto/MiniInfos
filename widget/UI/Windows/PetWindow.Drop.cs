using System.Windows;
using System.Windows.Media;
using MiniInfos.Mascot;

namespace MiniInfos.UI.Windows;

public partial class PetWindow
{
    private PetDropPose? DropAt(TimeSpan elapsed) => IsDropping ? _drop!.At(elapsed - _releasedAt!.Value) : null;
    private static PetState DropState(PetDropPose pose) => pose.Phase == PetDropPhase.Impact && pose.Landing == PetLanding.Head
        ? PetState.ProtectEyes : PetState.Active;

    private Matrix DropMatrix(PetDropPose pose)
    {
        var center = ToWindow(_appearance.RestCenter);
        var matrix = Matrix.Identity;
        matrix.Translate(-center.X, -center.Y);
        matrix.Scale(pose.ScaleX, pose.ScaleY);
        matrix.Rotate(pose.Angle);
        matrix.Translate(_dropStartCenter.X + pose.Offset.X, _dropStartCenter.Y + pose.Offset.Y);
        return matrix;
    }

    private Rect PixelBounds(ReadOnlySpan<byte> pixels)
    {
        var bounds = PetPixels.Bounds(pixels, PetFrame.CanvasSize);
        bounds.Scale(SpriteSize / PetFrame.CanvasSize, SpriteSize / PetFrame.CanvasSize);
        return bounds;
    }

    private void FinishDrop(TimeSpan now)
    {
        var matrix = _motion.Matrix;
        if (_state == PetState.HeldFeet)
        {
            // Canonicalize both pixels and transform: the release frame remains identical, but recovery can roll upright.
            _player.HalfTurn();
            var turn = Matrix.Identity;
            turn.RotateAt(180, SpriteSize / 2, SpriteSize / 2);
            turn.Append(matrix);
            matrix = turn;
        }
        var center = ToWindow(_appearance.RestCenter);
        var sourceBounds = PixelBounds(_player.Pixels.Span);
        var paintedBounds = sourceBounds;
        paintedBounds.Transform(matrix);
        var restBounds = PixelBounds(_appearance.Frame(PetState.Idle, 0).Pixels.Span);
        sourceBounds.Offset(-center.X, -center.Y);
        restBounds.Offset(-center.X, -center.Y);
        var scaleX = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
        var scaleY = matrix.Determinant / scaleX;
        var angle = Math.Atan2(matrix.M12, matrix.M11) * 180 / Math.PI;
        var dpi = VisualTreeHelper.GetDpi(this);
        var monitor = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(
            (int)_interaction.PointerScreen.X, (int)_interaction.PointerScreen.Y)).WorkingArea;
        var bottom = PointFromScreen(new Point(monitor.Left, monitor.Bottom)).Y;
        var distance = Math.Min(SpriteSize * 0.3, Math.Max(0, bottom - paintedBounds.Bottom - 6));
        var velocity = _interaction.ReleaseVelocity;
        velocity = new(velocity.X / dpi.DpiScaleX, velocity.Y / dpi.DpiScaleY);
        _dropStartCenter = matrix.Transform(center);
        _releaseOrigin = PointToScreen(new Point(0, 0));
        _drop = new(angle, _interaction.AngularVelocity, new(scaleX, scaleY), velocity,
            sourceBounds, restBounds, SpriteSize, distance);
        _releasedAt = now;
        var last = _drop.At(_drop.Duration);
        var end = _dropStartCenter + last.Offset - (Vector)center;
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        _dropPlacement = new PetPlacement(Left + end.X, Top + end.Y).KeepVisible(screen, SpriteSize);
        SavePosition();
    }

    private void CompleteDrop()
    {
        var target = _dropPlacement!;
        ClearDrop();
        Width = Height = SpriteSize;
        Left = target.Left;
        Top = target.Top;
        SavePosition();
    }

    private void ClearDrop()
    {
        _releasedAt = null;
        _drop = null;
        _dropPlacement = null;
        _suspendedDrop = false;
    }
}
