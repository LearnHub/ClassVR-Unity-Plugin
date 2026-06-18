using System.Collections.Generic;
using System.Threading;

namespace ClassVR.Tests.Common {
  /// <summary>
  /// Helpers for driving an <see cref="IAsyncEnumerable{T}"/> synchronously from a plain <c>[Test]</c> method.
  /// The Unity Test Framework version that ships with Unity 2021.3 (1.1.33) predates async-Task test support, so we
  /// pump the enumerator manually. This only deadlocks if a stage genuinely awaits I/O — the test fakes return
  /// already-completed Tasks, so every <c>MoveNextAsync</c> finishes inline.
  /// </summary>
  internal static class AsyncEnumerableTestExtensions {
    public static List<T> ToListSync<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default) {
      var list = new List<T>();
      var enumerator = source.GetAsyncEnumerator(cancellationToken);
      try {
        while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult()) {
          list.Add(enumerator.Current);
        }
      } finally {
        enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
      }
      return list;
    }
  }
}
