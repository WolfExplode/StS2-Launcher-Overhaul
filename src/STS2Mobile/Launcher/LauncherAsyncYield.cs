using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace STS2Mobile.Launcher;

internal static class LauncherAsyncYield
{
    internal static async Task<bool> ProcessFrameAsync(
        SceneTree tree,
        LauncherMonotonicDeadline deadline,
        LauncherOperationLifecycle lifecycle = null
    )
        => await CreateWaiter(tree, lifecycle).WaitForProcessFrameAsync(deadline)
            == LauncherAsyncWaitOutcome.Signaled;

    internal static async Task<bool> FramePostDrawAsync(
        LauncherMonotonicDeadline deadline,
        LauncherOperationLifecycle lifecycle = null
    )
        => await CreateWaiter(null, lifecycle).WaitForFramePostDrawAsync(deadline)
            == LauncherAsyncWaitOutcome.Signaled;

    internal static LauncherAsyncSignalWaiter CreateWaiter(
        SceneTree tree,
        LauncherOperationLifecycle lifecycle = null
    )
        => new(new GodotLauncherAsyncSignalSource(tree), lifecycle);

    private sealed class GodotLauncherAsyncSignalSource : ILauncherAsyncSignalSource
    {
        private readonly SceneTree _tree;

        internal GodotLauncherAsyncSignalSource(SceneTree tree)
        {
            _tree = tree;
        }

        public async Task WaitForProcessFrameAsync()
        {
            if (_tree == null)
            {
                await Task.Yield();
                return;
            }

            await _tree.ToSignal(_tree, SceneTree.SignalName.ProcessFrame);
        }

        public async Task WaitForFramePostDrawAsync()
            => await RenderingServer.Singleton.ToSignal(
                RenderingServer.Singleton,
                RenderingServer.SignalName.FramePostDraw
            );

        public Task WaitForDelayAsync(
            int milliseconds,
            CancellationToken cancellationToken
        )
            => Task.Delay(Math.Max(0, milliseconds), cancellationToken);
    }
}
