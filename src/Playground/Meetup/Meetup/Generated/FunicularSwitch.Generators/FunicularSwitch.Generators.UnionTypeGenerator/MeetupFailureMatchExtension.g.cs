#pragma warning disable 1591
namespace Meetup
{
	public static partial class FailureMatchExtension
	{
		public static T Match<T>(this Meetup.Failure failure, global::System.Func<Meetup.Failure.Cancelled_, T> cancelled, global::System.Func<Meetup.Failure.Conflict_, T> conflict, global::System.Func<Meetup.Failure.Forbidden_, T> forbidden, global::System.Func<Meetup.Failure.Internal_, T> @internal, global::System.Func<Meetup.Failure.InvalidInput_, T> invalidInput, global::System.Func<Meetup.Failure.Multiple_, T> multiple, global::System.Func<Meetup.Failure.NotFound_, T> notFound) =>
		failure switch
		{
			Meetup.Failure.Cancelled_ cancelled1 => cancelled(cancelled1),
			Meetup.Failure.Conflict_ conflict2 => conflict(conflict2),
			Meetup.Failure.Forbidden_ forbidden3 => forbidden(forbidden3),
			Meetup.Failure.Internal_ @internal4 => @internal(@internal4),
			Meetup.Failure.InvalidInput_ invalidInput5 => invalidInput(invalidInput5),
			Meetup.Failure.Multiple_ multiple6 => multiple(multiple6),
			Meetup.Failure.NotFound_ notFound7 => notFound(notFound7),
			_ => throw new global::System.ArgumentException($"Unknown type derived from Meetup.Failure: {failure.GetType().Name}")
		};
		
		public static global::System.Threading.Tasks.Task<T> Match<T>(this Meetup.Failure failure, global::System.Func<Meetup.Failure.Cancelled_, global::System.Threading.Tasks.Task<T>> cancelled, global::System.Func<Meetup.Failure.Conflict_, global::System.Threading.Tasks.Task<T>> conflict, global::System.Func<Meetup.Failure.Forbidden_, global::System.Threading.Tasks.Task<T>> forbidden, global::System.Func<Meetup.Failure.Internal_, global::System.Threading.Tasks.Task<T>> @internal, global::System.Func<Meetup.Failure.InvalidInput_, global::System.Threading.Tasks.Task<T>> invalidInput, global::System.Func<Meetup.Failure.Multiple_, global::System.Threading.Tasks.Task<T>> multiple, global::System.Func<Meetup.Failure.NotFound_, global::System.Threading.Tasks.Task<T>> notFound) =>
		failure switch
		{
			Meetup.Failure.Cancelled_ cancelled1 => cancelled(cancelled1),
			Meetup.Failure.Conflict_ conflict2 => conflict(conflict2),
			Meetup.Failure.Forbidden_ forbidden3 => forbidden(forbidden3),
			Meetup.Failure.Internal_ @internal4 => @internal(@internal4),
			Meetup.Failure.InvalidInput_ invalidInput5 => invalidInput(invalidInput5),
			Meetup.Failure.Multiple_ multiple6 => multiple(multiple6),
			Meetup.Failure.NotFound_ notFound7 => notFound(notFound7),
			_ => throw new global::System.ArgumentException($"Unknown type derived from Meetup.Failure: {failure.GetType().Name}")
		};
		
		public static async global::System.Threading.Tasks.Task<T> Match<T>(this global::System.Threading.Tasks.Task<Meetup.Failure> failure, global::System.Func<Meetup.Failure.Cancelled_, T> cancelled, global::System.Func<Meetup.Failure.Conflict_, T> conflict, global::System.Func<Meetup.Failure.Forbidden_, T> forbidden, global::System.Func<Meetup.Failure.Internal_, T> @internal, global::System.Func<Meetup.Failure.InvalidInput_, T> invalidInput, global::System.Func<Meetup.Failure.Multiple_, T> multiple, global::System.Func<Meetup.Failure.NotFound_, T> notFound) =>
		(await failure.ConfigureAwait(false)).Match(cancelled, conflict, forbidden, @internal, invalidInput, multiple, notFound);
		
		public static async global::System.Threading.Tasks.Task<T> Match<T>(this global::System.Threading.Tasks.Task<Meetup.Failure> failure, global::System.Func<Meetup.Failure.Cancelled_, global::System.Threading.Tasks.Task<T>> cancelled, global::System.Func<Meetup.Failure.Conflict_, global::System.Threading.Tasks.Task<T>> conflict, global::System.Func<Meetup.Failure.Forbidden_, global::System.Threading.Tasks.Task<T>> forbidden, global::System.Func<Meetup.Failure.Internal_, global::System.Threading.Tasks.Task<T>> @internal, global::System.Func<Meetup.Failure.InvalidInput_, global::System.Threading.Tasks.Task<T>> invalidInput, global::System.Func<Meetup.Failure.Multiple_, global::System.Threading.Tasks.Task<T>> multiple, global::System.Func<Meetup.Failure.NotFound_, global::System.Threading.Tasks.Task<T>> notFound) =>
		await (await failure.ConfigureAwait(false)).Match(cancelled, conflict, forbidden, @internal, invalidInput, multiple, notFound).ConfigureAwait(false);
		
		public static void Switch(this Meetup.Failure failure, global::System.Action<Meetup.Failure.Cancelled_> cancelled, global::System.Action<Meetup.Failure.Conflict_> conflict, global::System.Action<Meetup.Failure.Forbidden_> forbidden, global::System.Action<Meetup.Failure.Internal_> @internal, global::System.Action<Meetup.Failure.InvalidInput_> invalidInput, global::System.Action<Meetup.Failure.Multiple_> multiple, global::System.Action<Meetup.Failure.NotFound_> notFound)
		{
			switch (failure)
			{
				case Meetup.Failure.Cancelled_ cancelled1:
					cancelled(cancelled1);
					break;
				case Meetup.Failure.Conflict_ conflict2:
					conflict(conflict2);
					break;
				case Meetup.Failure.Forbidden_ forbidden3:
					forbidden(forbidden3);
					break;
				case Meetup.Failure.Internal_ @internal4:
					@internal(@internal4);
					break;
				case Meetup.Failure.InvalidInput_ invalidInput5:
					invalidInput(invalidInput5);
					break;
				case Meetup.Failure.Multiple_ multiple6:
					multiple(multiple6);
					break;
				case Meetup.Failure.NotFound_ notFound7:
					notFound(notFound7);
					break;
				default:
					throw new global::System.ArgumentException($"Unknown type derived from Meetup.Failure: {failure.GetType().Name}");
			}
		}
		
		public static async global::System.Threading.Tasks.Task Switch(this Meetup.Failure failure, global::System.Func<Meetup.Failure.Cancelled_, global::System.Threading.Tasks.Task> cancelled, global::System.Func<Meetup.Failure.Conflict_, global::System.Threading.Tasks.Task> conflict, global::System.Func<Meetup.Failure.Forbidden_, global::System.Threading.Tasks.Task> forbidden, global::System.Func<Meetup.Failure.Internal_, global::System.Threading.Tasks.Task> @internal, global::System.Func<Meetup.Failure.InvalidInput_, global::System.Threading.Tasks.Task> invalidInput, global::System.Func<Meetup.Failure.Multiple_, global::System.Threading.Tasks.Task> multiple, global::System.Func<Meetup.Failure.NotFound_, global::System.Threading.Tasks.Task> notFound)
		{
			switch (failure)
			{
				case Meetup.Failure.Cancelled_ cancelled1:
					await cancelled(cancelled1).ConfigureAwait(false);
					break;
				case Meetup.Failure.Conflict_ conflict2:
					await conflict(conflict2).ConfigureAwait(false);
					break;
				case Meetup.Failure.Forbidden_ forbidden3:
					await forbidden(forbidden3).ConfigureAwait(false);
					break;
				case Meetup.Failure.Internal_ @internal4:
					await @internal(@internal4).ConfigureAwait(false);
					break;
				case Meetup.Failure.InvalidInput_ invalidInput5:
					await invalidInput(invalidInput5).ConfigureAwait(false);
					break;
				case Meetup.Failure.Multiple_ multiple6:
					await multiple(multiple6).ConfigureAwait(false);
					break;
				case Meetup.Failure.NotFound_ notFound7:
					await notFound(notFound7).ConfigureAwait(false);
					break;
				default:
					throw new global::System.ArgumentException($"Unknown type derived from Meetup.Failure: {failure.GetType().Name}");
			}
		}
		
		public static async global::System.Threading.Tasks.Task Switch(this global::System.Threading.Tasks.Task<Meetup.Failure> failure, global::System.Action<Meetup.Failure.Cancelled_> cancelled, global::System.Action<Meetup.Failure.Conflict_> conflict, global::System.Action<Meetup.Failure.Forbidden_> forbidden, global::System.Action<Meetup.Failure.Internal_> @internal, global::System.Action<Meetup.Failure.InvalidInput_> invalidInput, global::System.Action<Meetup.Failure.Multiple_> multiple, global::System.Action<Meetup.Failure.NotFound_> notFound) =>
		(await failure.ConfigureAwait(false)).Switch(cancelled, conflict, forbidden, @internal, invalidInput, multiple, notFound);
		
		public static async global::System.Threading.Tasks.Task Switch(this global::System.Threading.Tasks.Task<Meetup.Failure> failure, global::System.Func<Meetup.Failure.Cancelled_, global::System.Threading.Tasks.Task> cancelled, global::System.Func<Meetup.Failure.Conflict_, global::System.Threading.Tasks.Task> conflict, global::System.Func<Meetup.Failure.Forbidden_, global::System.Threading.Tasks.Task> forbidden, global::System.Func<Meetup.Failure.Internal_, global::System.Threading.Tasks.Task> @internal, global::System.Func<Meetup.Failure.InvalidInput_, global::System.Threading.Tasks.Task> invalidInput, global::System.Func<Meetup.Failure.Multiple_, global::System.Threading.Tasks.Task> multiple, global::System.Func<Meetup.Failure.NotFound_, global::System.Threading.Tasks.Task> notFound) =>
		await (await failure.ConfigureAwait(false)).Switch(cancelled, conflict, forbidden, @internal, invalidInput, multiple, notFound).ConfigureAwait(false);
	}
	
	public abstract partial record Failure
	{
		public static Meetup.Failure Cancelled(string message = null) => new Meetup.Failure.Cancelled_(message);
		public static Meetup.Failure Conflict(string Message) => new Meetup.Failure.Conflict_(Message);
		public static Meetup.Failure Forbidden(string Message) => new Meetup.Failure.Forbidden_(Message);
		public static Meetup.Failure Internal(string Message) => new Meetup.Failure.Internal_(Message);
		public static Meetup.Failure InvalidInput(string Message) => new Meetup.Failure.InvalidInput_(Message);
		public static Meetup.Failure Multiple(global::System.Collections.Generic.IReadOnlyCollection<global::Meetup.Failure> Failures) => new Meetup.Failure.Multiple_(Failures);
		public static Meetup.Failure NotFound(string Message) => new Meetup.Failure.NotFound_(Message);
	}
}
#pragma warning restore 1591
