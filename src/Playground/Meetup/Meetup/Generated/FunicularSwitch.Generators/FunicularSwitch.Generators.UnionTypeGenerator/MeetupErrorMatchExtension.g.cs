#pragma warning disable 1591
namespace Meetup
{
	public static partial class ErrorMatchExtension
	{
		public static T Match<T>(this Meetup.Error error, global::System.Func<Meetup.Error.Cancelled_, T> cancelled, global::System.Func<Meetup.Error.Conflict_, T> conflict, global::System.Func<Meetup.Error.Forbidden_, T> forbidden, global::System.Func<Meetup.Error.Internal_, T> @internal, global::System.Func<Meetup.Error.InvalidInput_, T> invalidInput, global::System.Func<Meetup.Error.Multiple_, T> multiple, global::System.Func<Meetup.Error.NotFound_, T> notFound) =>
		error switch
		{
			Meetup.Error.Cancelled_ cancelled1 => cancelled(cancelled1),
			Meetup.Error.Conflict_ conflict2 => conflict(conflict2),
			Meetup.Error.Forbidden_ forbidden3 => forbidden(forbidden3),
			Meetup.Error.Internal_ @internal4 => @internal(@internal4),
			Meetup.Error.InvalidInput_ invalidInput5 => invalidInput(invalidInput5),
			Meetup.Error.Multiple_ multiple6 => multiple(multiple6),
			Meetup.Error.NotFound_ notFound7 => notFound(notFound7),
			_ => throw new global::System.ArgumentException($"Unknown type derived from Meetup.Error: {error.GetType().Name}")
		};
		
		public static global::System.Threading.Tasks.Task<T> Match<T>(this Meetup.Error error, global::System.Func<Meetup.Error.Cancelled_, global::System.Threading.Tasks.Task<T>> cancelled, global::System.Func<Meetup.Error.Conflict_, global::System.Threading.Tasks.Task<T>> conflict, global::System.Func<Meetup.Error.Forbidden_, global::System.Threading.Tasks.Task<T>> forbidden, global::System.Func<Meetup.Error.Internal_, global::System.Threading.Tasks.Task<T>> @internal, global::System.Func<Meetup.Error.InvalidInput_, global::System.Threading.Tasks.Task<T>> invalidInput, global::System.Func<Meetup.Error.Multiple_, global::System.Threading.Tasks.Task<T>> multiple, global::System.Func<Meetup.Error.NotFound_, global::System.Threading.Tasks.Task<T>> notFound) =>
		error switch
		{
			Meetup.Error.Cancelled_ cancelled1 => cancelled(cancelled1),
			Meetup.Error.Conflict_ conflict2 => conflict(conflict2),
			Meetup.Error.Forbidden_ forbidden3 => forbidden(forbidden3),
			Meetup.Error.Internal_ @internal4 => @internal(@internal4),
			Meetup.Error.InvalidInput_ invalidInput5 => invalidInput(invalidInput5),
			Meetup.Error.Multiple_ multiple6 => multiple(multiple6),
			Meetup.Error.NotFound_ notFound7 => notFound(notFound7),
			_ => throw new global::System.ArgumentException($"Unknown type derived from Meetup.Error: {error.GetType().Name}")
		};
		
		public static async global::System.Threading.Tasks.Task<T> Match<T>(this global::System.Threading.Tasks.Task<Meetup.Error> error, global::System.Func<Meetup.Error.Cancelled_, T> cancelled, global::System.Func<Meetup.Error.Conflict_, T> conflict, global::System.Func<Meetup.Error.Forbidden_, T> forbidden, global::System.Func<Meetup.Error.Internal_, T> @internal, global::System.Func<Meetup.Error.InvalidInput_, T> invalidInput, global::System.Func<Meetup.Error.Multiple_, T> multiple, global::System.Func<Meetup.Error.NotFound_, T> notFound) =>
		(await error.ConfigureAwait(false)).Match(cancelled, conflict, forbidden, @internal, invalidInput, multiple, notFound);
		
		public static async global::System.Threading.Tasks.Task<T> Match<T>(this global::System.Threading.Tasks.Task<Meetup.Error> error, global::System.Func<Meetup.Error.Cancelled_, global::System.Threading.Tasks.Task<T>> cancelled, global::System.Func<Meetup.Error.Conflict_, global::System.Threading.Tasks.Task<T>> conflict, global::System.Func<Meetup.Error.Forbidden_, global::System.Threading.Tasks.Task<T>> forbidden, global::System.Func<Meetup.Error.Internal_, global::System.Threading.Tasks.Task<T>> @internal, global::System.Func<Meetup.Error.InvalidInput_, global::System.Threading.Tasks.Task<T>> invalidInput, global::System.Func<Meetup.Error.Multiple_, global::System.Threading.Tasks.Task<T>> multiple, global::System.Func<Meetup.Error.NotFound_, global::System.Threading.Tasks.Task<T>> notFound) =>
		await (await error.ConfigureAwait(false)).Match(cancelled, conflict, forbidden, @internal, invalidInput, multiple, notFound).ConfigureAwait(false);
		
		public static void Switch(this Meetup.Error error, global::System.Action<Meetup.Error.Cancelled_> cancelled, global::System.Action<Meetup.Error.Conflict_> conflict, global::System.Action<Meetup.Error.Forbidden_> forbidden, global::System.Action<Meetup.Error.Internal_> @internal, global::System.Action<Meetup.Error.InvalidInput_> invalidInput, global::System.Action<Meetup.Error.Multiple_> multiple, global::System.Action<Meetup.Error.NotFound_> notFound)
		{
			switch (error)
			{
				case Meetup.Error.Cancelled_ cancelled1:
					cancelled(cancelled1);
					break;
				case Meetup.Error.Conflict_ conflict2:
					conflict(conflict2);
					break;
				case Meetup.Error.Forbidden_ forbidden3:
					forbidden(forbidden3);
					break;
				case Meetup.Error.Internal_ @internal4:
					@internal(@internal4);
					break;
				case Meetup.Error.InvalidInput_ invalidInput5:
					invalidInput(invalidInput5);
					break;
				case Meetup.Error.Multiple_ multiple6:
					multiple(multiple6);
					break;
				case Meetup.Error.NotFound_ notFound7:
					notFound(notFound7);
					break;
				default:
					throw new global::System.ArgumentException($"Unknown type derived from Meetup.Error: {error.GetType().Name}");
			}
		}
		
		public static async global::System.Threading.Tasks.Task Switch(this Meetup.Error error, global::System.Func<Meetup.Error.Cancelled_, global::System.Threading.Tasks.Task> cancelled, global::System.Func<Meetup.Error.Conflict_, global::System.Threading.Tasks.Task> conflict, global::System.Func<Meetup.Error.Forbidden_, global::System.Threading.Tasks.Task> forbidden, global::System.Func<Meetup.Error.Internal_, global::System.Threading.Tasks.Task> @internal, global::System.Func<Meetup.Error.InvalidInput_, global::System.Threading.Tasks.Task> invalidInput, global::System.Func<Meetup.Error.Multiple_, global::System.Threading.Tasks.Task> multiple, global::System.Func<Meetup.Error.NotFound_, global::System.Threading.Tasks.Task> notFound)
		{
			switch (error)
			{
				case Meetup.Error.Cancelled_ cancelled1:
					await cancelled(cancelled1).ConfigureAwait(false);
					break;
				case Meetup.Error.Conflict_ conflict2:
					await conflict(conflict2).ConfigureAwait(false);
					break;
				case Meetup.Error.Forbidden_ forbidden3:
					await forbidden(forbidden3).ConfigureAwait(false);
					break;
				case Meetup.Error.Internal_ @internal4:
					await @internal(@internal4).ConfigureAwait(false);
					break;
				case Meetup.Error.InvalidInput_ invalidInput5:
					await invalidInput(invalidInput5).ConfigureAwait(false);
					break;
				case Meetup.Error.Multiple_ multiple6:
					await multiple(multiple6).ConfigureAwait(false);
					break;
				case Meetup.Error.NotFound_ notFound7:
					await notFound(notFound7).ConfigureAwait(false);
					break;
				default:
					throw new global::System.ArgumentException($"Unknown type derived from Meetup.Error: {error.GetType().Name}");
			}
		}
		
		public static async global::System.Threading.Tasks.Task Switch(this global::System.Threading.Tasks.Task<Meetup.Error> error, global::System.Action<Meetup.Error.Cancelled_> cancelled, global::System.Action<Meetup.Error.Conflict_> conflict, global::System.Action<Meetup.Error.Forbidden_> forbidden, global::System.Action<Meetup.Error.Internal_> @internal, global::System.Action<Meetup.Error.InvalidInput_> invalidInput, global::System.Action<Meetup.Error.Multiple_> multiple, global::System.Action<Meetup.Error.NotFound_> notFound) =>
		(await error.ConfigureAwait(false)).Switch(cancelled, conflict, forbidden, @internal, invalidInput, multiple, notFound);
		
		public static async global::System.Threading.Tasks.Task Switch(this global::System.Threading.Tasks.Task<Meetup.Error> error, global::System.Func<Meetup.Error.Cancelled_, global::System.Threading.Tasks.Task> cancelled, global::System.Func<Meetup.Error.Conflict_, global::System.Threading.Tasks.Task> conflict, global::System.Func<Meetup.Error.Forbidden_, global::System.Threading.Tasks.Task> forbidden, global::System.Func<Meetup.Error.Internal_, global::System.Threading.Tasks.Task> @internal, global::System.Func<Meetup.Error.InvalidInput_, global::System.Threading.Tasks.Task> invalidInput, global::System.Func<Meetup.Error.Multiple_, global::System.Threading.Tasks.Task> multiple, global::System.Func<Meetup.Error.NotFound_, global::System.Threading.Tasks.Task> notFound) =>
		await (await error.ConfigureAwait(false)).Switch(cancelled, conflict, forbidden, @internal, invalidInput, multiple, notFound).ConfigureAwait(false);
	}
	
	public abstract partial record Error
	{
		public static Meetup.Error Cancelled(string message = null) => new Meetup.Error.Cancelled_(message);
		public static Meetup.Error Conflict(string Message) => new Meetup.Error.Conflict_(Message);
		public static Meetup.Error Forbidden(string Message) => new Meetup.Error.Forbidden_(Message);
		public static Meetup.Error Internal(string Message) => new Meetup.Error.Internal_(Message);
		public static Meetup.Error InvalidInput(string Message) => new Meetup.Error.InvalidInput_(Message);
		public static Meetup.Error Multiple(global::System.Collections.Generic.IReadOnlyCollection<global::Meetup.Error> Errors) => new Meetup.Error.Multiple_(Errors);
		public static Meetup.Error NotFound(string Message) => new Meetup.Error.NotFound_(Message);
	}
}
#pragma warning restore 1591
