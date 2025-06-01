using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BestDelivery;
using GeoPoint = BestDelivery.Point;
using DisplayPoint = System.Windows.Point;
using System.Globalization;

namespace Orders
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Order[] activparc = Array.Empty<Order>();
        private GeoPoint hubloc;
        private int[] delivorder = Array.Empty<int>();
        private readonly Random rnd = new Random();
        public MainWindow()
        {
            InitializeComponent();
            canvas.MouseLeftButtonDown += NewOrder;
        }
        private void DelivScen(Func<Order[]> fetchOrders, string description)
        {
            activparc = fetchOrders();
            hubloc = activparc.First(o => o.ID == -1).Destination;
            delivorder = CreateOptimizedRoute(activparc, hubloc);
            RefreshlistViewOrders();
            UpdateRouteInfo();
        }
        private void center_Click(object sender, RoutedEventArgs e) =>
    DelivScen(OrderArrays.GetOrderArray1, "Центр");
        private void far_center_Click(object sender, RoutedEventArgs e) =>
    DelivScen(OrderArrays.GetOrderArray2, "Дальше от центра");
        private void district_Click(object sender, RoutedEventArgs e) =>
    DelivScen(OrderArrays.GetOrderArray3, "Один район");
        private void different_parts_Click(object sender, RoutedEventArgs e) =>
    DelivScen(OrderArrays.GetOrderArray4, "Разные районы");
        private void different_priority_Click(object sender, RoutedEventArgs e) =>
    DelivScen(OrderArrays.GetOrderArray5, "Разные приоритеты");
        private void more_orders_Click(object sender, RoutedEventArgs e) =>
    DelivScen(OrderArrays.GetOrderArray6, "Много заказов");

        private void RefreshlistViewOrders()
        {
            listViewOrders.Items.Clear();

            foreach (var order in activparc)
            {
                listViewOrders.Items.Add(new
                {
                    OrderNumber = order.ID == -1 ? "Склад" : $"Заказ #{order.ID}",
                    Destination = $"({order.Destination.X:F2}, {order.Destination.Y:F2})",
                    Priority = order.ID == -1 ? "" : order.Priority.ToString("F2")
                });
            }
        }

        
        public class OrderViewModel
        {
            public string OrderNumber { get; set; }
            public string Destination { get; set; }
            public string Priority { get; set; }
        }
        private void UpdateRouteInfo()
        {
            if (Valid(hubloc, activparc, delivorder, out double routeLength))
            {
                Cost.Text = $"Оптимальный путь: {routeLength:F2}";
                Route.Text = "Маршрут: " + string.Join(" → ", delivorder.Select(id => id == -1 ? "Склад" : "#" + id));
                DrawRoute();
            }
            else
            {
                Cost.Text = "Маршрут недействителен";
                Route.Text = "";
                canvas.Children.Clear();
            }
        }

        private void DrawRoute()
        {
            canvas.Children.Clear();
            var positions = new Dictionary<int, DisplayPoint>();
            double margin = 50;
            double width = canvas.ActualWidth > 0 ? canvas.ActualWidth : 800;
            double height = canvas.ActualHeight > 0 ? canvas.ActualHeight : 600;
            var points = activparc.Select(p => p.Destination).ToList();
            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);
            double scaleX = (width - 2 * margin) / (maxX - minX);
            double scaleY = (height - 2 * margin) / (maxY - minY);
            double scale = Math.Min(scaleX, scaleY);
            double shiftX = margin - minX * scale;
            double shiftY = height - margin + minY * scale;
            DisplayPoint Map(GeoPoint p) => new(p.X * scale + shiftX, shiftY - p.Y * scale);

            foreach (var order in activparc)
            {
                var point = Map(order.Destination);
                positions[order.ID] = point;

                var marker = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = order.ID == -1 ? Brushes.Red : Brushes.Black, 
                    Stroke = Brushes.Black,
                    StrokeThickness = 1.5
                };
                Canvas.SetLeft(marker, point.X - 6);
                Canvas.SetTop(marker, point.Y - 6);
                canvas.Children.Add(marker);
            }

            for (int i = 0; i < delivorder.Length - 1; i++)
            {
                var from = positions[delivorder[i]];
                var to = positions[delivorder[i + 1]];
                var line = new Line
                {
                    X1 = from.X,
                    Y1 = from.Y,
                    X2 = to.X,
                    Y2 = to.Y,
                    Stroke = Brushes.Black, 
                    StrokeThickness = 2
                };
                canvas.Children.Add(line);
            }
        }
        

        private void NewOrder(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var pos = e.GetPosition(canvas);

                // Если canvas ещё не инициализирован по размерам
                if (canvas.ActualWidth == 0 || canvas.ActualHeight == 0)
                {
                    MessageBox.Show("Подождите, пока график полностью загрузится");
                    return;
                }

                // Получаем границы текущих заказов
                var points = activparc.Select(p => p.Destination).ToList();
                if (!points.Any())
                {
                    MessageBox.Show("Сначала загрузите сценарий заказов");
                    return;
                }

                double minX = points.Min(p => p.X);
                double maxX = points.Max(p => p.X);
                double minY = points.Min(p => p.Y);
                double maxY = points.Max(p => p.Y);

                // Рассчитываем масштаб
                double margin = 50;
                double width = canvas.ActualWidth;
                double height = canvas.ActualHeight;

                double scaleX = (width - 2 * margin) / (maxX - minX);
                double scaleY = (height - 2 * margin) / (maxY - minY);
                double scale = Math.Min(scaleX, scaleY);

                double shiftX = margin - minX * scale;
                double shiftY = height - margin + minY * scale;

                // Преобразуем координаты клика
                double x = (pos.X - shiftX) / scale;
                double y = (shiftY - pos.Y) / scale;

                // Проверяем границы
                if (double.IsNaN(x) || double.IsNaN(y))
                {
                    MessageBox.Show("Ошибка в расчете координат");
                    return;
                }

                // Запрос приоритета
                string input = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Новый заказ в ({x:F2}, {y:F2})\nВведите приоритет (0.0-1.0):",
                    "Добавить заказ",
                    "0.5");

                if (string.IsNullOrEmpty(input)) return;

                if (!double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out double priority) ||
                    priority < 0 || priority > 1)
                {
                    MessageBox.Show("Приоритет должен быть от 0.0 до 1.0");
                    return;
                }

                // Добавляем заказ
                int newId = activparc.Max(o => o.ID) + 1;
                var newOrder = new Order
                {
                    ID = newId,
                    Destination = new GeoPoint { X = x, Y = y },
                    Priority = priority
                };

                activparc = activparc.Append(newOrder).ToArray();
                delivorder = CreateOptimizedRoute(activparc, hubloc);

                // Обновляем интерфейс
                RefreshlistViewOrders();
                DrawRoute();
                UpdateRouteInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }
        public static int[] CreateOptimizedRoute(Order[] parcels, GeoPoint hub)
        {
            var orders = parcels.Where(p => p.ID != -1).ToList();
            if (orders.Count == 0) return [-1, -1];

            var points = new List<GeoPoint> { hub };
            points.AddRange(orders.Select(o => o.Destination));

            int n = points.Count;
            double[,] dist = new double[n, n];

            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    dist[i, j] = i == j ? 0 : Math.Sqrt(Math.Pow(points[i].X - points[j].X, 2) + Math.Pow(points[i].Y - points[j].Y, 2));

            List<int> route = new() { 0 };
            var unvisited = Enumerable.Range(1, n - 1).ToList();

            while (unvisited.Count > 0)
            {
                int last = route.Last();
                int next = unvisited.OrderBy(i => dist[last, i]).First();
                route.Add(next);
                unvisited.Remove(next);
            }

            route.Add(0);

            var result = new List<int> { -1 };
            for (int i = 1; i < route.Count - 1; i++)
                result.Add(orders[route[i] - 1].ID);
            result.Add(-1);
            return result.ToArray();
        }
        public static bool Valid(GeoPoint hub, Order[] parcels, int[] route, out double routeLength)
        {
            routeLength = 0;
            if (parcels == null || route == null || parcels.Length == 0 || route.Length == 0) return false;

            var routeList = new List<int>(route);
            if (routeList.First() != -1 || routeList.Last() != -1) return false;

            var allIds = parcels.Where(p => p.ID != -1).Select(p => p.ID).ToHashSet();
            var visited = routeList.Where(id => id != -1).ToHashSet();
            if (!allIds.SetEquals(visited)) return false;

            GeoPoint current = hub;
            foreach (var id in routeList.Skip(1))
            {
                var o = parcels.First(p => p.ID == id);
                routeLength += Math.Sqrt(Math.Pow(current.X - o.Destination.X, 2) + Math.Pow(current.Y - o.Destination.Y, 2));
                current = o.Destination;
            }
            return true;
        }
    }
}