namespace Skinnix.Compoetry.Maui;

public class FlyoutPageWrapper : FlyoutPage
{
	private readonly NavigationPage navigation;

	public FlyoutPageWrapper(Page rootPage)
	{
		this.FlyoutLayoutBehavior = FlyoutLayoutBehavior.Popover;
		//this.FlowDirection = FlowDirection.RightToLeft;

		Flyout = new ContentPage()
		{
			FlowDirection = FlowDirection.LeftToRight,
			Title = "Flyout Title",
			Content = new Button()
			{
				Text = "Flyout"
			}
		};

		Detail = navigation = new NavigationPage(rootPage)
		{
			FlowDirection = FlowDirection.LeftToRight,
		};

		//Detail = new ContentPage()
		//{
		//	Content = new Button()
		//	{
		//		Text = "DetailContent"
		//	}
		//};
	}

	//protected override void OnAppearing()
	//{
	//	base.OnAppearing();

	//	navigation.PushAsync(new Pages.Document.DocumentPage());
	//}
}